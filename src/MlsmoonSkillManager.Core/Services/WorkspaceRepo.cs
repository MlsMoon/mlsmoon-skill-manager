using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class WorkspaceRepo
{
    private static readonly Dictionary<string, string> QuietGit = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GIT_TERMINAL_PROMPT"] = "0",
        ["GIT_SSH_COMMAND"] = "ssh -o BatchMode=yes -o ConnectTimeout=5 -o StrictHostKeyChecking=accept-new"
    };

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
        CancellationToken cancellationToken = default)
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
        await FetchAsync(directory, branch, cancellationToken).ConfigureAwait(false);
        await AlignHeadIfMatchAsync(directory, branch, cancellationToken).ConfigureAwait(false);
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

        var head = await _workspaceGit.ReadHeadCommitAsync(directory, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(head))
        {
            await RunGitAsync(directory, ["checkout", "-f", "-B", branch, upstream], cancellationToken, required: true)
                .ConfigureAwait(false);
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

    public async Task AlignHeadIfMatchAsync(
        string directory,
        string branch,
        CancellationToken cancellationToken = default)
    {
        if (!WorkspaceGit.HasRepo(directory) || string.IsNullOrWhiteSpace(branch))
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
        CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(dest))
        {
            log?.Invoke("在安装目录里 fetch 并快进。");
            await EnsureAttachedAsync(dest, originUrl, branch, log, cancellationToken).ConfigureAwait(false);
            await FastForwardAsync(dest, branch, cancellationToken).ConfigureAwait(false);
            return;
        }

        SkillCopy.Replace(source, dest, includeGit: true);
        await EnsureAttachedAsync(dest, originUrl, branch, log, cancellationToken).ConfigureAwait(false);
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
