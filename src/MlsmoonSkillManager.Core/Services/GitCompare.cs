using System.Text.Json;
using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class GitCompare
{
    private readonly IProcessRunner _runner;

    public GitCompare(IProcessRunner runner)
    {
        _runner = runner;
    }

    public async Task<CommitCompare> CompareAsync(
        SkillDefinition skill,
        string localSha,
        string remoteSha,
        string? cacheDirectory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(localSha) && string.IsNullOrWhiteSpace(remoteSha))
        {
            return CommitCompare.Same;
        }

        if (string.IsNullOrWhiteSpace(localSha) || string.IsNullOrWhiteSpace(remoteSha))
        {
            return CommitCompare.Unknown;
        }

        if (localSha.Equals(remoteSha, StringComparison.OrdinalIgnoreCase))
        {
            return CommitCompare.Same;
        }

        if (!skill.IsLan && RepoUrl.TryParse(skill.Repo, out var repo))
        {
            var github = await CompareGitHubAsync(repo, localSha, remoteSha, cancellationToken)
                .ConfigureAwait(false);
            if (github.Relation != CommitRelation.Unknown)
            {
                return github;
            }
        }

        if (!string.IsNullOrWhiteSpace(cacheDirectory)
            && Directory.Exists(Path.Combine(cacheDirectory, ".git")))
        {
            return await CompareLocalAsync(cacheDirectory, localSha, remoteSha, cancellationToken)
                .ConfigureAwait(false);
        }

        return CommitCompare.Unknown;
    }

    private async Task<CommitCompare> CompareGitHubAsync(
        GitHubRepoRef repo,
        string localSha,
        string remoteSha,
        CancellationToken cancellationToken)
    {
        var path = $"repos/{repo.OwnerRepo}/compare/{Uri.EscapeDataString(localSha)}...{Uri.EscapeDataString(remoteSha)}";
        var result = await _runner.RunAsync(
                "gh",
                ["api", path],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success || string.IsNullOrWhiteSpace(result.StdOut))
        {
            return CommitCompare.Unknown;
        }

        try
        {
            using var doc = JsonDocument.Parse(result.StdOut);
            var root = doc.RootElement;
            var status = root.TryGetProperty("status", out var statusEl)
                ? statusEl.GetString() ?? ""
                : "";
            var aheadBy = ReadCount(root, "ahead_by");
            var behindBy = ReadCount(root, "behind_by");
            return status.ToLowerInvariant() switch
            {
                "identical" => CommitCompare.Same,
                "ahead" => CommitCompare.Behind(aheadBy),
                "behind" => CommitCompare.Ahead(behindBy),
                "diverged" => CommitCompare.Fork(behindBy, aheadBy),
                _ => CommitCompare.Unknown
            };
        }
        catch (JsonException)
        {
            return CommitCompare.Unknown;
        }
    }

    private async Task<CommitCompare> CompareLocalAsync(
        string directory,
        string localSha,
        string remoteSha,
        CancellationToken cancellationToken)
    {
        await EnsureCommitAsync(directory, localSha, cancellationToken).ConfigureAwait(false);
        await EnsureCommitAsync(directory, remoteSha, cancellationToken).ConfigureAwait(false);

        var counted = await _runner.RunAsync(
                "git",
                ["-C", directory, "rev-list", "--left-right", "--count", localSha + "..." + remoteSha],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (counted.Success && TryParseLeftRight(counted.StdOut, out var aheadBy, out var behindBy))
        {
            if (aheadBy == 0 && behindBy == 0)
            {
                return CommitCompare.Same;
            }

            if (aheadBy == 0)
            {
                return CommitCompare.Behind(behindBy);
            }

            if (behindBy == 0)
            {
                return CommitCompare.Ahead(aheadBy);
            }

            return CommitCompare.Fork(aheadBy, behindBy);
        }

        var localIsAncestor = await IsAncestorAsync(directory, localSha, remoteSha, cancellationToken)
            .ConfigureAwait(false);
        var remoteIsAncestor = await IsAncestorAsync(directory, remoteSha, localSha, cancellationToken)
            .ConfigureAwait(false);
        if (localIsAncestor == true && remoteIsAncestor != true)
        {
            return CommitCompare.Behind();
        }

        if (remoteIsAncestor == true && localIsAncestor != true)
        {
            return CommitCompare.Ahead();
        }

        if (localIsAncestor == false && remoteIsAncestor == false)
        {
            return CommitCompare.Fork();
        }

        return CommitCompare.Unknown;
    }

    private async Task EnsureCommitAsync(string directory, string sha, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sha))
        {
            return;
        }

        var exists = await _runner.RunAsync(
                "git",
                ["-C", directory, "cat-file", "-e", sha + "^{commit}"],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (exists.Success)
        {
            return;
        }

        await _runner.RunAsync(
                "git",
                ["-C", directory, "fetch", "origin", sha, "--depth", "1"],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool?> IsAncestorAsync(
        string directory,
        string ancestor,
        string descendant,
        CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(
                "git",
                ["-C", directory, "merge-base", "--is-ancestor", ancestor, descendant],
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode switch
        {
            0 => true,
            1 => false,
            _ => null
        };
    }

    private static int ReadCount(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out var value) && value.TryGetInt32(out var n)
            ? n
            : 0;
    }

    private static bool TryParseLeftRight(string text, out int left, out int right)
    {
        left = 0;
        right = 0;
        var parts = text.Trim().Split(['\t', ' '], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
               && int.TryParse(parts[0], out left)
               && int.TryParse(parts[1], out right);
    }
}
