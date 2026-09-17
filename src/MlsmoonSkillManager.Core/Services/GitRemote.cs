using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class GitRemote
{
    private static readonly Dictionary<string, string> QuietGit = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GIT_TERMINAL_PROMPT"] = "0",
        ["GIT_SSH_COMMAND"] = "ssh -o BatchMode=yes -o ConnectTimeout=5 -o StrictHostKeyChecking=accept-new"
    };

    private readonly IProcessRunner _runner;

    public GitRemote(IProcessRunner runner)
    {
        _runner = runner;
    }

    public async Task<RepoAccess> CheckLanAccessAsync(
        SkillDefinition skill,
        string user,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(skill.Host))
        {
            return new RepoAccess
            {
                State = AccessState.NoPermission,
                OwnerRepo = skill.DisplayName,
                Visibility = "局域网",
                Message = "局域网条目缺少 host，无法探测。"
            };
        }

        var host = skill.Host.Trim();
        var probe = await LanNetwork.ProbeAsync(host, cancellationToken).ConfigureAwait(false);
        if (!probe.OnLan)
        {
            return new RepoAccess
            {
                State = AccessState.OffNetwork,
                OwnerRepo = skill.DisplayName,
                Visibility = "局域网",
                Message = probe.Detail
            };
        }

        if (!probe.HostReachable)
        {
            return new RepoAccess
            {
                State = AccessState.Unreachable,
                OwnerRepo = skill.DisplayName,
                Visibility = "局域网",
                Message = probe.Detail
            };
        }

        string url;
        try
        {
            url = skill.SshUrl(user);
        }
        catch (Exception ex)
        {
            return new RepoAccess
            {
                State = AccessState.NoPermission,
                OwnerRepo = skill.DisplayName,
                Message = ex.Message
            };
        }

        var remote = await _runner.RunAsync(
                "git",
                ["ls-remote", url, "HEAD"],
                cancellationToken: cancellationToken,
                environment: QuietGit)
            .ConfigureAwait(false);

        if (remote.Success && !string.IsNullOrWhiteSpace(remote.StdOut))
        {
            return new RepoAccess
            {
                State = AccessState.Accessible,
                OwnerRepo = url,
                Visibility = "NAS",
                Message = "NAS 可访问"
            };
        }

        var state = ClassifySshFailure(remote.StdErr + " " + remote.StdOut, hostReachable: true);
        return new RepoAccess
        {
            State = state,
            OwnerRepo = url,
            Visibility = "局域网",
            Message = state switch
            {
                AccessState.NeedsAuth =>
                    "NAS 可达，但 SSH 需要密码或密钥。应用不会弹出密码，请先配好密钥，或在终端执行 ssh。",
                AccessState.Unreachable => "NAS 在探测时可达，但 git 连不上，请再试一次。",
                _ => "NAS 无访问权限。检查用户名、仓库路径和 SSH 配置。"
            }
        };
    }

    public static AccessState ClassifySshFailure(string detail, bool hostReachable)
    {
        if (!hostReachable)
        {
            return AccessState.Unreachable;
        }

        var text = detail.ToLowerInvariant();
        if (ContainsAny(text, "permission denied", "publickey", "password", "authentication failed",
                "too many authentication", "no more authentication methods",
                "host key verification failed", "invalid format"))
        {
            return AccessState.NeedsAuth;
        }

        if (ContainsAny(text, "connection refused", "connection timed out", "timed out",
                "network is unreachable", "no route to host", "could not resolve",
                "connection reset", "unable to connect", "connection closed"))
        {
            return AccessState.Unreachable;
        }

        return AccessState.NoPermission;
    }

    private static bool ContainsAny(string text, params string[] parts)
    {
        return parts.Any(part => text.Contains(part, StringComparison.Ordinal));
    }

    public async Task<string> TryReadReadmeAsync(
        SkillDefinition skill,
        string user,
        string branch,
        CancellationToken cancellationToken = default)
    {
        var file = string.IsNullOrWhiteSpace(skill.ReadmePath) ? "Readme.md" : skill.ReadmePath;
        var url = skill.SshUrl(user);
        var tar = Path.Combine(Path.GetTempPath(), "msm-lan-" + Guid.NewGuid().ToString("N") + ".tar");
        try
        {
            var archive = await _runner.RunAsync(
                    "git",
                    ["archive", "--remote=" + url, branch, file, "-o", tar],
                    cancellationToken: cancellationToken,
                    environment: QuietGit)
                .ConfigureAwait(false);
            if (!archive.Success || !File.Exists(tar))
            {
                return "";
            }

            var extract = await _runner.RunAsync(
                    "tar",
                    ["-xOf", tar, file],
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return extract.Success ? extract.StdOut : "";
        }
        finally
        {
            try
            {
                if (File.Exists(tar))
                {
                    File.Delete(tar);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    public static string Excerpt(string markdown, int maxChars = 200)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return "";
        }

        var parts = new List<string>();
        foreach (var raw in markdown.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                if (parts.Count > 0)
                {
                    break;
                }

                continue;
            }

            if (line.StartsWith('#'))
            {
                continue;
            }

            parts.Add(line.Trim('*'));
            if (string.Join(" ", parts).Length >= maxChars)
            {
                break;
            }
        }

        var text = string.Join(" ", parts).Trim();
        return text.Length <= maxChars ? text : text[..maxChars].TrimEnd() + "…";
    }

    public async Task<IReadOnlyList<GitHead>> ListRemoteHeadsAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(
                "git",
                ["ls-remote", "--heads", url],
                cancellationToken: cancellationToken,
                environment: QuietGit)
            .ConfigureAwait(false);
        return ParseHeads(result.StdOut);
    }

    public async Task<string> RemoteTipAsync(
        string url,
        string branch,
        CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(
                "git",
                ["ls-remote", url, "refs/heads/" + branch],
                cancellationToken: cancellationToken,
                environment: QuietGit)
            .ConfigureAwait(false);
        return ParseHeads(result.StdOut).FirstOrDefault()?.Sha ?? "";
    }

    public static IReadOnlyList<GitHead> ParseHeads(string stdout)
    {
        var list = new List<GitHead>();
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return list;
        }

        foreach (var raw in stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var tab = raw.IndexOfAny(['\t', ' ']);
            if (tab <= 0)
            {
                continue;
            }

            var sha = raw[..tab].Trim();
            var name = raw[(tab + 1)..].Trim();
            if (name.StartsWith("refs/heads/", StringComparison.Ordinal))
            {
                name = name["refs/heads/".Length..];
            }

            if (!string.IsNullOrWhiteSpace(sha) && !string.IsNullOrWhiteSpace(name))
            {
                list.Add(new GitHead(name, sha));
            }
        }

        return list;
    }

    public async Task CloneOrUpdateAsync(
        string url,
        string directory,
        string branch,
        Action<string>? log,
        CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(Path.Combine(directory, ".git")))
        {
            log?.Invoke($"更新局域网缓存 {branch}");
            await _runner.RunAsync("git", ["-C", directory, "fetch", "--depth", "1", "origin", branch],
                    cancellationToken: cancellationToken, environment: QuietGit)
                .ConfigureAwait(false);
            var checkout = await _runner.RunAsync(
                    "git",
                    ["-C", directory, "checkout", "-B", branch, "FETCH_HEAD"],
                    cancellationToken: cancellationToken,
                    environment: QuietGit)
                .ConfigureAwait(false);
            if (checkout.Success)
            {
                return;
            }

            log?.Invoke($"缓存更新失败，改为重新克隆：{checkout.StdErr}");
            Directory.Delete(directory, true);
        }

        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(directory)!);
        log?.Invoke($"克隆 {url}（{branch}）");
        var clone = await _runner.RunAsync(
                "git",
                ["clone", "--depth", "1", "--branch", branch, url, directory],
                cancellationToken: cancellationToken,
                environment: QuietGit)
            .ConfigureAwait(false);
        if (!clone.Success)
        {
            throw new InvalidOperationException($"克隆失败 {url}: {clone.StdErr}");
        }
    }

    public Task<string> ReadHeadCommitAsync(string directory, CancellationToken cancellationToken = default)
    {
        return ReadCommitAsync(directory, cancellationToken);
    }

    private async Task<string> ReadCommitAsync(string directory, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
                "git",
                ["-C", directory, "rev-parse", "HEAD"],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.Success ? result.StdOut.Trim() : "";
    }
}

public sealed record GitHead(string Name, string Sha);
