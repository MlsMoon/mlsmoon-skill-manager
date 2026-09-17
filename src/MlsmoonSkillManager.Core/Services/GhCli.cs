using System.Text.Json;
using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class GhCli
{
    private readonly IProcessRunner _runner;

    public GhCli(IProcessRunner runner)
    {
        _runner = runner;
    }

    public async Task<GhAccountStatus> GetAccountStatusAsync(CancellationToken cancellationToken = default)
    {
        var version = await _runner.RunAsync("gh", ["--version"], cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!version.Success)
        {
            return new GhAccountStatus
            {
                GhInstalled = false,
                Detail = "未找到 GitHub CLI（gh）。请先安装 https://cli.github.com/ 并保证在 PATH 中。"
            };
        }

        var user = await _runner.RunAsync("gh", ["api", "user", "--jq", ".login"], cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!user.Success || string.IsNullOrWhiteSpace(user.StdOut))
        {
            return new GhAccountStatus
            {
                GhInstalled = true,
                LoggedIn = false,
                Detail = "已安装 gh，但当前未登录。请在本机执行 gh auth login。"
            };
        }

        return new GhAccountStatus
        {
            GhInstalled = true,
            LoggedIn = true,
            Login = user.StdOut.Trim(),
            Detail = $"已登录 GitHub：{user.StdOut.Trim()}"
        };
    }

    public async Task<RepoAccess> CheckRepoAccessAsync(
        GitHubRepoRef repo,
        GhAccountStatus account,
        CancellationToken cancellationToken = default)
    {
        if (!account.GhInstalled)
        {
            return new RepoAccess
            {
                State = AccessState.GhMissing,
                OwnerRepo = repo.OwnerRepo,
                Message = account.Detail
            };
        }

        var result = await _runner.RunAsync(
                "gh",
                ["repo", "view", repo.OwnerRepo, "--json", "name,visibility,isPrivate"],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (result.Success)
        {
            var visibility = ReadVisibility(result.StdOut);
            return new RepoAccess
            {
                State = AccessState.Accessible,
                OwnerRepo = repo.OwnerRepo,
                Visibility = visibility,
                Message = visibility.Equals("PRIVATE", StringComparison.OrdinalIgnoreCase)
                    ? "已确认可访问该私有仓库"
                    : "可访问"
            };
        }

        if (!account.LoggedIn)
        {
            return new RepoAccess
            {
                State = AccessState.GhNotLoggedIn,
                OwnerRepo = repo.OwnerRepo,
                Message = "当前 gh 未登录，无法确认私有 Skill 权限"
            };
        }

        return new RepoAccess
        {
            State = AccessState.NoPermission,
            OwnerRepo = repo.OwnerRepo,
            Message = "当前无权限访问"
        };
    }

    public async Task<IReadOnlyList<string>> ListBranchesAsync(
        GitHubRepoRef repo,
        CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(
                "gh",
                ["api", $"repos/{repo.OwnerRepo}/branches", "--paginate", "--jq", ".[].name"],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success || string.IsNullOrWhiteSpace(result.StdOut))
        {
            return [];
        }

        return result.StdOut
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<string> DefaultBranchAsync(
        GitHubRepoRef repo,
        CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(
                "gh",
                ["api", $"repos/{repo.OwnerRepo}", "--jq", ".default_branch"],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.Success ? result.StdOut.Trim() : "";
    }

    public async Task<string> BranchCommitAsync(
        GitHubRepoRef repo,
        string branch,
        CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(
                "gh",
                ["api", $"repos/{repo.OwnerRepo}/commits/{Uri.EscapeDataString(branch)}", "--jq", ".sha"],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.Success ? result.StdOut.Trim() : "";
    }

    public async Task CloneOrUpdateAsync(
        GitHubRepoRef repo,
        string cacheDirectory,
        Action<string>? log = null,
        string? branch = null,
        CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(Path.Combine(cacheDirectory, ".git")))
        {
            log?.Invoke(string.IsNullOrWhiteSpace(branch)
                ? $"更新缓存 {repo.OwnerRepo}"
                : $"更新缓存 {repo.OwnerRepo}（{branch}）");
            if (!string.IsNullOrWhiteSpace(branch))
            {
                var fetch = await _runner.RunAsync(
                        "git",
                        ["-C", cacheDirectory, "fetch", "origin", branch],
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (fetch.Success)
                {
                    var checkout = await _runner.RunAsync(
                            "git",
                            ["-C", cacheDirectory, "checkout", "-B", branch, "FETCH_HEAD"],
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);
                    if (checkout.Success)
                    {
                        return;
                    }
                }

                log?.Invoke($"缓存更新失败，改为重新克隆：{fetch.StdErr}");
            }
            else
            {
                var pull = await _runner.RunAsync(
                        "git",
                        ["-C", cacheDirectory, "pull", "--ff-only"],
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                if (pull.Success)
                {
                    return;
                }

                log?.Invoke($"缓存更新失败，改为重新克隆：{pull.StdErr}");
            }

            Directory.Delete(cacheDirectory, true);
        }

        if (Directory.Exists(cacheDirectory))
        {
            Directory.Delete(cacheDirectory, true);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(cacheDirectory)!);
        log?.Invoke(string.IsNullOrWhiteSpace(branch)
            ? $"克隆 {repo.OwnerRepo}"
            : $"克隆 {repo.OwnerRepo}（{branch}）");
        var cloneArgs = new List<string> { "repo", "clone", repo.OwnerRepo, cacheDirectory, "--", "--depth", "1" };
        if (!string.IsNullOrWhiteSpace(branch))
        {
            cloneArgs.Add("--branch");
            cloneArgs.Add(branch);
        }

        var clone = await _runner.RunAsync("gh", cloneArgs, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!clone.Success)
        {
            throw new InvalidOperationException(
                $"克隆失败 {repo.OwnerRepo}: {FirstLine(clone.StdErr, clone.StdOut)}");
        }
    }

    public async Task<string> ReadCurrentBranchAsync(
        string cacheDirectory,
        CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(
                "git",
                ["-C", cacheDirectory, "branch", "--show-current"],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.Success ? result.StdOut.Trim() : "";
    }

    public async Task<string> ReadHeadCommitAsync(
        string cacheDirectory,
        CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(
                "git",
                ["-C", cacheDirectory, "rev-parse", "HEAD"],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.Success ? result.StdOut.Trim() : "";
    }

    private static string ReadVisibility(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("visibility", out var visibility))
            {
                return visibility.GetString() ?? "";
            }

            if (doc.RootElement.TryGetProperty("isPrivate", out var isPrivate)
                && isPrivate.GetBoolean())
            {
                return "PRIVATE";
            }
        }
        catch (JsonException)
        {
        }

        return "PUBLIC";
    }

    private static string FirstLine(params string[] texts)
    {
        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var line = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(line))
            {
                return line;
            }
        }

        return "未知错误";
    }
}
