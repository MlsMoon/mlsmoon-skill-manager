using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class WorkspaceRepo
{
    private static IReadOnlyDictionary<string, string> QuietGit => GitSsh.Variables();

    private static readonly string[] MarkerNames =
    [
        SkillInstaller.MarkerFileName,
        SkillInstaller.PluginMarkerFileName,
        SkillInstaller.PackageMarkerFileName
    ];

    private readonly IProcessRunner _runner;
    private readonly WorkspaceGit _workspaceGit;

    public WorkspaceRepo(IProcessRunner runner)
    {
        _runner = runner;
        _workspaceGit = new WorkspaceGit(runner);
    }

    public static bool CanAttach(SkillDefinition skill) => skill.IsRepoRoot;

    public static string? OriginUrl(SkillDefinition skill, string? nasUser)
    {
        if (skill.IsLan)
        {
            try
            {
                return skill.SshUrl(nasUser ?? "");
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        return RepoUrl.TryParse(skill.Repo, out var repo) ? repo.HttpsUrl : null;
    }

    public async Task EnsureAttachedAsync(
        string directory,
        string originUrl,
        string branch,
        Action<string>? log,
        CancellationToken cancellationToken = default,
        bool fetch = true)
    {
        if (!Directory.Exists(directory) || string.IsNullOrWhiteSpace(originUrl))
        {
            return;
        }

        if (!WorkspaceGit.HasRepo(directory))
        {
            log?.Invoke("工作区没有 .git，正在初始化并接上远端。");
            await RunGitAsync(directory, ["init"], cancellationToken, required: true).ConfigureAwait(false);
        }

        await EnsureOriginAsync(directory, originUrl, cancellationToken).ConfigureAwait(false);
        ExcludeMarkers(directory);
        if (!fetch)
        {
            return;
        }

        await FetchAsync(directory, branch, cancellationToken).ConfigureAwait(false);
        await AlignHeadIfMatchAsync(directory, branch, cancellationToken).ConfigureAwait(false);
    }

    public async Task AdoptRemoteAsync(
        string directory,
        string originUrl,
        string branch,
        Action<string>? log,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(originUrl) || string.IsNullOrWhiteSpace(branch))
        {
            throw new InvalidOperationException("没有远端地址或分支，无法初始化 Git。");
        }

        if (!Directory.Exists(directory))
        {
            throw new InvalidOperationException("安装目录不存在，无法初始化 Git。");
        }

        log?.Invoke("正在把安装目录接到远端分支（不走 checkout，以免未跟踪文件挡住）。");
        progress?.Report(new ScanProgress("正在接上远端…", 12));
        await EnsureAttachedAsync(directory, originUrl, branch, log, cancellationToken, fetch: false)
            .ConfigureAwait(false);
        progress?.Report(new ScanProgress("正在从远端拉取…", 28));
        await FetchAsync(directory, branch, cancellationToken).ConfigureAwait(false);
        progress?.Report(new ScanProgress("正在按远端覆盖同名文件…", 72));
        await PointHeadAtOriginAsync(directory, branch, cancellationToken).ConfigureAwait(false);
        progress?.Report(new ScanProgress("已把 HEAD 接到 origin/" + branch, 100));
        log?.Invoke("已把 HEAD 接到 origin/" + branch + "。");
    }

    public async Task FetchAsync(string directory, string branch, CancellationToken cancellationToken = default)
    {
        if (!WorkspaceGit.HasRepo(directory) || string.IsNullOrWhiteSpace(branch))
        {
            return;
        }

        await RunGitAsync(
                directory,
                ["fetch", "origin", $"+{branch}:refs/remotes/origin/{branch}"],
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task FastForwardAsync(
        string directory,
        string branch,
        CancellationToken cancellationToken = default)
    {
        if (!WorkspaceGit.HasRepo(directory) || string.IsNullOrWhiteSpace(branch))
        {
            throw new InvalidOperationException("安装目录还不是 git 仓库，无法快进。");
        }

        await FetchAsync(directory, branch, cancellationToken).ConfigureAwait(false);
        if (!await _workspaceGit.HasHeadAsync(directory, cancellationToken).ConfigureAwait(false))
        {
            await PointHeadAtOriginAsync(directory, branch, cancellationToken).ConfigureAwait(false);
            return;
        }

        var current = await _workspaceGit.ReadCurrentBranchAsync(directory, cancellationToken)
            .ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(current)
            && !current.Equals(branch, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"当前在分支 {current}，不能对 {branch} 做快进。请先切换分支。");
        }

        var upstream = await UpstreamAsync(directory, branch, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(upstream))
        {
            throw new InvalidOperationException("没有取到远端分支，无法快进。");
        }

        if (await _workspaceGit.MatchesCommitAsync(directory, upstream, cancellationToken).ConfigureAwait(false) == true)
        {
            await CheckoutBranchAsync(directory, branch, upstream, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!await IsCleanAsync(directory, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("工作区有本地修改，不能快进。请在安装目录里手动处理。");
        }

        var merge = await RunGitAsync(
                directory,
                ["merge", "--ff-only", upstream],
                cancellationToken)
            .ConfigureAwait(false);
        if (!merge.Success)
        {
            throw new InvalidOperationException("不能快进：" + FirstLine(merge.StdErr, merge.StdOut));
        }
    }

    public async Task<bool> IsCleanAsync(string directory, CancellationToken cancellationToken = default)
    {
        var status = await RunGitAsync(directory, ["status", "--porcelain"], cancellationToken).ConfigureAwait(false);
        if (!status.Success)
        {
            return false;
        }

        return status.StdOut
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Length >= 3 ? line[3..].Trim() : line.Trim())
            .All(path => MarkerNames.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase));
    }

    public async Task SwitchBranchAsync(
        string directory,
        string branch,
        Action<string>? log,
        CancellationToken cancellationToken = default)
    {
        if (!WorkspaceGit.HasRepo(directory) || string.IsNullOrWhiteSpace(branch))
        {
            throw new InvalidOperationException("安装目录还不是 git 仓库，无法切换分支。");
        }

        var current = await _workspaceGit.ReadCurrentBranchAsync(directory, cancellationToken)
            .ConfigureAwait(false);
        if (current.Equals(branch, StringComparison.OrdinalIgnoreCase))
        {
            log?.Invoke("已经在分支 " + branch + "。");
            return;
        }

        if (!await IsCleanAsync(directory, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("工作区有本地修改，不能切换分支。请在安装目录里手动处理。");
        }

        await FetchAsync(directory, branch, cancellationToken).ConfigureAwait(false);
        var localRef = "refs/heads/" + branch;
        var remoteRef = "origin/" + branch;
        if (await _workspaceGit.HasRefAsync(directory, localRef, cancellationToken).ConfigureAwait(false))
        {
            log?.Invoke("正在切换到本地分支 " + branch + "。");
            await RunGitAsync(directory, ["checkout", branch], cancellationToken, required: true)
                .ConfigureAwait(false);
            return;
        }

        if (!await _workspaceGit.HasRefAsync(directory, remoteRef, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("没有远端分支 origin/" + branch + "，无法切换。");
        }

        log?.Invoke("正在创建并切换到 " + branch + "（跟踪 origin/" + branch + "）。");
        await RunGitAsync(
                directory,
                ["checkout", "--track", remoteRef],
                cancellationToken,
                required: true)
            .ConfigureAwait(false);
    }

    public async Task AlignHeadIfMatchAsync(
        string directory,
        string branch,
        CancellationToken cancellationToken = default)
    {
        if (!WorkspaceGit.HasRepo(directory) || string.IsNullOrWhiteSpace(branch))
        {
            return;
        }

        if (!await _workspaceGit.HasHeadAsync(directory, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var current = await _workspaceGit.ReadCurrentBranchAsync(directory, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(current)
            || !current.Equals(branch, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var upstream = await UpstreamAsync(directory, branch, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(upstream))
        {
            return;
        }

        if (await _workspaceGit.MatchesCommitAsync(directory, upstream, cancellationToken).ConfigureAwait(false) != true)
        {
            return;
        }

        await CheckoutBranchAsync(directory, branch, upstream, cancellationToken).ConfigureAwait(false);
    }

    public async Task SyncSkillAsync(
        string source,
        string dest,
        string originUrl,
        string branch,
        Action<string>? log,
        CancellationToken cancellationToken = default,
        ISet<string>? extraSkip = null)
    {
        if (WorkspaceGit.HasRepo(dest))
        {
            log?.Invoke("安装目录已有 .git，fetch 并快进。");
            await EnsureAttachedAsync(dest, originUrl, branch, log, cancellationToken).ConfigureAwait(false);
            await FastForwardAsync(dest, branch, cancellationToken).ConfigureAwait(false);
            return;
        }

        log?.Invoke("复制仓库（含 .git）");
        SkillCopy.Replace(source, dest, extraSkip, includeGit: true);
        await EnsureAttachedAsync(dest, originUrl, branch, log, cancellationToken).ConfigureAwait(false);
    }

    public async Task CommitAsync(
        string directory,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new InvalidOperationException("请填写提交说明。");
        }

        if (!WorkspaceGit.HasRepo(directory))
        {
            throw new InvalidOperationException("安装目录还不是 git 仓库，无法提交。");
        }

        await RunGitAsync(directory, ["add", "-A"], cancellationToken, required: true).ConfigureAwait(false);
        var commit = await RunGitAsync(directory, ["commit", "-m", message.Trim()], cancellationToken)
            .ConfigureAwait(false);
        if (!commit.Success)
        {
            throw new InvalidOperationException("提交失败：" + FirstLine(commit.StdErr, commit.StdOut));
        }
    }

    public async Task PushAsync(
        string directory,
        string branch,
        CancellationToken cancellationToken = default)
    {
        if (!WorkspaceGit.HasRepo(directory))
        {
            throw new InvalidOperationException("安装目录还不是 git 仓库，无法 Push。");
        }

        if (string.IsNullOrWhiteSpace(branch))
        {
            throw new InvalidOperationException("没有分支，无法 Push。");
        }

        var push = await RunGitAsync(directory, ["push", "-u", "origin", branch], cancellationToken)
            .ConfigureAwait(false);
        if (!push.Success)
        {
            throw new InvalidOperationException("Push 失败：" + FirstLine(push.StdErr, push.StdOut));
        }
    }

    private async Task EnsureOriginAsync(string directory, string originUrl, CancellationToken cancellationToken)
    {
        var remote = await RunGitAsync(directory, ["remote"], cancellationToken).ConfigureAwait(false);
        var names = remote.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (names.Contains("origin", StringComparer.OrdinalIgnoreCase))
        {
            await RunGitAsync(directory, ["remote", "set-url", "origin", originUrl], cancellationToken, required: true)
                .ConfigureAwait(false);
            return;
        }

        await RunGitAsync(directory, ["remote", "add", "origin", originUrl], cancellationToken, required: true)
            .ConfigureAwait(false);
    }

    private async Task<string> UpstreamAsync(string directory, string branch, CancellationToken cancellationToken)
    {
        var origin = "origin/" + branch;
        var verify = await RunGitAsync(directory, ["rev-parse", "--verify", origin], cancellationToken)
            .ConfigureAwait(false);
        if (verify.Success)
        {
            return origin;
        }

        var fetchHead = await RunGitAsync(directory, ["rev-parse", "--verify", "FETCH_HEAD"], cancellationToken)
            .ConfigureAwait(false);
        return fetchHead.Success ? "FETCH_HEAD" : "";
    }

    private async Task PointHeadAtOriginAsync(
        string directory,
        string branch,
        CancellationToken cancellationToken)
    {
        var origin = "origin/" + branch;
        var sha = await RunGitAsync(directory, ["rev-parse", "--verify", origin], cancellationToken)
            .ConfigureAwait(false);
        if (!sha.Success)
        {
            throw new InvalidOperationException("远端还没有 " + origin + "，无法初始化 Git。");
        }

        var commit = sha.StdOut.Trim();
        await RunGitAsync(directory, ["update-ref", "refs/heads/" + branch, commit], cancellationToken, required: true)
            .ConfigureAwait(false);
        await RunGitAsync(directory, ["symbolic-ref", "HEAD", "refs/heads/" + branch], cancellationToken, required: true)
            .ConfigureAwait(false);
        await RunGitAsync(directory, ["reset", "--hard"], cancellationToken, required: true)
            .ConfigureAwait(false);
        await RunGitAsync(directory, ["branch", "-u", origin, branch], cancellationToken, required: true)
            .ConfigureAwait(false);
    }

    private async Task CheckoutBranchAsync(
        string directory,
        string branch,
        string upstream,
        CancellationToken cancellationToken)
    {
        await RunGitAsync(directory, ["checkout", "-B", branch, upstream], cancellationToken).ConfigureAwait(false);
        var origin = "origin/" + branch;
        if (origin.Equals(upstream, StringComparison.OrdinalIgnoreCase)
            || await RunGitAsync(directory, ["rev-parse", "--verify", origin], cancellationToken).ConfigureAwait(false)
                is { Success: true })
        {
            await RunGitAsync(directory, ["branch", "-u", origin, branch], cancellationToken).ConfigureAwait(false);
        }
    }

    private static void ExcludeMarkers(string directory)
    {
        var exclude = Path.Combine(directory, ".git", "info", "exclude");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(exclude)!);
            var existing = File.Exists(exclude) ? File.ReadAllText(exclude) : "";
            var add = MarkerNames.Where(name =>
                existing.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0);
            var extra = string.Join(Environment.NewLine, add);
            if (extra.Length > 0)
            {
                File.AppendAllText(exclude, Environment.NewLine + extra + Environment.NewLine);
            }
        }
        catch (IOException)
        {
        }
    }

    private async Task<ProcessResult> RunGitAsync(
        string directory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        bool required = false)
    {
        var args = new List<string> { "-C", directory };
        args.AddRange(arguments);
        var result = await _runner.RunAsync(
                "git",
                args,
                cancellationToken: cancellationToken,
                environment: QuietGit)
            .ConfigureAwait(false);
        if (required && !result.Success)
        {
            throw new InvalidOperationException("git " + arguments[0] + " 失败：" + FirstLine(result.StdErr, result.StdOut));
        }

        return result;
    }

    private static string FirstLine(string error, string output)
    {
        var text = string.IsNullOrWhiteSpace(error) ? output : error;
        var line = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(line) ? "未知错误" : line;
    }
}
