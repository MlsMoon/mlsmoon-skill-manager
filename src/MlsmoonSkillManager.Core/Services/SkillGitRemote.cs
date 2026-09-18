using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class SkillGitRemote
{
    private readonly AppPaths _paths;
    private readonly GhCli _gh;
    private readonly GitRemote _git;
    private readonly Dictionary<string, IReadOnlyList<string>> _branchCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _tipCache = new(StringComparer.OrdinalIgnoreCase);

    public SkillGitRemote(AppPaths paths, GhCli gh, GitRemote git)
    {
        _paths = paths;
        _gh = gh;
        _git = git;
    }

    public void Clear()
    {
        _branchCache.Clear();
        _tipCache.Clear();
    }

    public async Task<IReadOnlyList<string>> ListBranchesAsync(
        SkillDefinition skill,
        string? nasUser,
        bool canReachRemote,
        CancellationToken cancellationToken = default)
    {
        var key = BranchKey(skill, nasUser);
        if (_branchCache.TryGetValue(key, out var cached) && cached.Count > 0)
        {
            return cached;
        }

        var names = new List<string>();
        foreach (var branch in skill.Branches)
        {
            if (!string.IsNullOrWhiteSpace(branch.Name)
                && !names.Contains(branch.Name, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(branch.Name);
            }
        }

        if (canReachRemote)
        {
            await AddRemoteBranchesAsync(skill, nasUser, names, cancellationToken).ConfigureAwait(false);
        }

        if (names.Count > 0)
        {
            _branchCache[key] = names;
        }

        return names;
    }

    public async Task<string> RemoteCommitAsync(
        SkillDefinition skill,
        string branch,
        string? nasUser,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(branch))
        {
            return "";
        }

        var key = BranchKey(skill, nasUser) + "|" + branch;
        if (_tipCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var sha = await ReadRemoteTipAsync(skill, branch, nasUser, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(sha))
        {
            _tipCache[key] = sha;
        }

        return sha;
    }

    public void RememberRemote(SkillDefinition skill, string? nasUser, string branch, string commit)
    {
        if (string.IsNullOrWhiteSpace(branch) || string.IsNullOrWhiteSpace(commit))
        {
            return;
        }

        _tipCache[BranchKey(skill, nasUser) + "|" + branch] = commit;
    }

    public string? CacheDirectory(SkillDefinition skill, string? branch)
    {
        if (skill.IsLan && !string.IsNullOrWhiteSpace(branch))
        {
            return _paths.LanCacheDirectory(skill.Host, skill.ResolvedInstallName, branch);
        }

        return RepoUrl.TryParse(skill.Repo, out var repo)
            ? _paths.RepoCacheDirectory(repo)
            : null;
    }

    private async Task AddRemoteBranchesAsync(
        SkillDefinition skill,
        string? nasUser,
        List<string> names,
        CancellationToken cancellationToken)
    {
        if (skill.IsLan)
        {
            try
            {
                var heads = await _git.ListRemoteHeadsAsync(skill.SshUrl(nasUser ?? ""), cancellationToken)
                    .ConfigureAwait(false);
                foreach (var head in heads)
                {
                    if (!names.Contains(head.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        names.Add(head.Name);
                    }
                }
            }
            catch (InvalidOperationException)
            {
            }

            return;
        }

        if (!RepoUrl.TryParse(skill.Repo, out var repo))
        {
            return;
        }

        var remote = await _gh.ListBranchesAsync(repo, cancellationToken).ConfigureAwait(false);
        foreach (var name in remote)
        {
            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(name);
            }
        }

        if (names.Count > 0)
        {
            return;
        }

        var fallback = await _gh.DefaultBranchAsync(repo, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(fallback))
        {
            names.Add(fallback);
        }
    }

    private async Task<string> ReadRemoteTipAsync(
        SkillDefinition skill,
        string branch,
        string? nasUser,
        CancellationToken cancellationToken)
    {
        if (skill.IsLan)
        {
            try
            {
                return await _git.RemoteTipAsync(skill.SshUrl(nasUser ?? ""), branch, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                return "";
            }
        }

        return RepoUrl.TryParse(skill.Repo, out var repo)
            ? await _gh.BranchCommitAsync(repo, branch, cancellationToken).ConfigureAwait(false)
            : "";
    }

    private static string BranchKey(SkillDefinition skill, string? nasUser)
    {
        return skill.IsLan ? skill.SshUrl(nasUser ?? "") : skill.Repo;
    }
}
