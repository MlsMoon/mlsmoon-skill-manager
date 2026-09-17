using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class SkillGit
{
    private readonly AppPaths _paths;
    private readonly GhCli _gh;
    private readonly GitRemote _git;
    private readonly GitCompare _compare;
    private readonly Dictionary<string, IReadOnlyList<string>> _branchCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _tipCache = new(StringComparer.OrdinalIgnoreCase);

    public SkillGit(AppPaths paths, GhCli gh, GitRemote git, IProcessRunner? runner = null)
    {
        _paths = paths;
        _gh = gh;
        _git = git;
        _compare = new GitCompare(runner ?? new ProcessRunner());
    }

    public static string RecommendBranch(SkillDefinition skill, string? editorVersion, string? installedBranch)
    {
        if (!string.IsNullOrWhiteSpace(installedBranch))
        {
            return installedBranch.Trim();
        }

        var picked = UnityWorkspace.PickBranch(skill, editorVersion);
        if (picked is not null && !string.IsNullOrWhiteSpace(picked.Name))
        {
            return picked.Name;
        }

        return skill.Branches.FirstOrDefault()?.Name ?? "";
    }

    public static bool IsForbiddenBranch(SkillDefinition skill, string? editorVersion, string branch)
    {
        return skill.IsLan
               && !string.IsNullOrWhiteSpace(editorVersion)
               && editorVersion.StartsWith("6000", StringComparison.Ordinal)
               && branch.Equals("master", StringComparison.OrdinalIgnoreCase);
    }

    public void ClearCaches()
    {
        _branchCache.Clear();
        _tipCache.Clear();
    }

    public async Task<SkillGitStatus> InspectAsync(
        SkillDefinition skill,
        string workspacePath,
        IReadOnlyList<RootInstallStatus> installs,
        string? selectedBranch,
        string? nasUser,
        string? editorVersion,
        bool canReachRemote,
        CancellationToken cancellationToken = default)
    {
        var installed = installs.Any(item => item.Installed);
        var copies = installs.Where(item => item.Installed).ToList();
        var first = copies.FirstOrDefault() ?? installs.FirstOrDefault();
        var installedBranch = first?.Branch ?? "";
        var installedCommit = first?.Commit ?? "";
        var branches = await ListBranchesAsync(skill, nasUser, canReachRemote, cancellationToken).ConfigureAwait(false);
        var target = string.IsNullOrWhiteSpace(selectedBranch)
            ? RecommendBranch(skill, editorVersion, installedBranch)
            : selectedBranch.Trim();
        if (string.IsNullOrWhiteSpace(target) && branches.Count > 0)
        {
            target = branches[0];
        }

        if (!string.IsNullOrWhiteSpace(target)
            && !branches.Contains(target, StringComparer.OrdinalIgnoreCase))
        {
            branches = [target, .. branches];
        }

        var remoteCommit = "";
        if (canReachRemote && !string.IsNullOrWhiteSpace(target))
        {
            remoteCommit = await RemoteCommitAsync(skill, target, nasUser, cancellationToken).ConfigureAwait(false);
        }

        var changes = new List<GitChange>();
        if (copies.Count > 0 && !string.IsNullOrWhiteSpace(workspacePath))
        {
            foreach (var item in copies)
            {
                var snap = _paths.SnapshotPath(workspacePath, skill.Id, item.Root);
                await TrySeedSnapshotAsync(skill, snap, item, cancellationToken).ConfigureAwait(false);
                var found = InstallSnapshot.Read(snap) is null
                    ? []
                    : await Task.Run(() => InstallSnapshot.Diff(snap, item.Path), cancellationToken)
                        .ConfigureAwait(false);
                if (copies.Count == 1)
                {
                    changes.AddRange(found);
                    continue;
                }

                changes.AddRange(found.Select(change => new GitChange
                {
                    Kind = change.Kind,
                    Path = item.Root + ": " + change.Path
                }));
            }
        }

        var hasLocal = changes.Count > 0;
        var branchDiffers = installed
                            && !string.IsNullOrWhiteSpace(installedBranch)
                            && !string.IsNullOrWhiteSpace(target)
                            && !installedBranch.Equals(target, StringComparison.OrdinalIgnoreCase);
        var compare = string.IsNullOrWhiteSpace(remoteCommit)
            ? CommitCompare.Unknown
            : await _compare.CompareAsync(
                    skill,
                    installedCommit,
                    remoteCommit,
                    CacheDirectory(skill, installedBranch),
                    cancellationToken)
                .ConfigureAwait(false);
        var forbidden = IsForbiddenBranch(skill, editorVersion, target);
        var state = SkillGitStatus.Decide(installed, hasLocal, compare.Relation, branchDiffers);

        return new SkillGitStatus
        {
            State = state,
            Compare = compare,
            InstalledCommit = installedCommit,
            RemoteCommit = remoteCommit,
            InstalledBranch = installedBranch,
            TargetBranch = target,
            Branches = branches,
            Changes = changes,
            Forbidden = forbidden,
            Warning = forbidden ? "Unity 6 不能使用 master 上的 URP 14，请改选 urp-17.5。" : "",
            Message = SkillGitStatus.Describe(
                state,
                compare,
                target,
                installedBranch,
                installedCommit,
                remoteCommit,
                changes.Count,
                canReachRemote,
                remoteCommit.Length > 0)
        };
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
            }
            else if (RepoUrl.TryParse(skill.Repo, out var repo))
            {
                var remote = await _gh.ListBranchesAsync(repo, cancellationToken).ConfigureAwait(false);
                foreach (var name in remote)
                {
                    if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        names.Add(name);
                    }
                }

                if (names.Count == 0)
                {
                    var fallback = await _gh.DefaultBranchAsync(repo, cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(fallback))
                    {
                        names.Add(fallback);
                    }
                }
            }
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

        string sha;
        if (skill.IsLan)
        {
            try
            {
                sha = await _git.RemoteTipAsync(skill.SshUrl(nasUser ?? ""), branch, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                return "";
            }
        }
        else if (RepoUrl.TryParse(skill.Repo, out var repo))
        {
            sha = await _gh.BranchCommitAsync(repo, branch, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            return "";
        }

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

    private async Task TrySeedSnapshotAsync(
        SkillDefinition skill,
        string snapshotPath,
        RootInstallStatus install,
        CancellationToken cancellationToken)
    {
        if (InstallSnapshot.Read(snapshotPath) is not null || !Directory.Exists(install.Path))
        {
            return;
        }

        var cache = CacheDirectory(skill, install.Branch);
        if (string.IsNullOrWhiteSpace(cache) || !Directory.Exists(Path.Combine(cache, ".git")))
        {
            return;
        }

        var head = await _git.ReadHeadCommitAsync(cache, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(install.Commit)
            || !head.Equals(install.Commit, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var source = SkillInstaller.ResolveSource(
                cache,
                skill.ResolvedSourcePath,
                skill.DisplayName,
                requireSkillMarkdown: false);
            await Task.Run(
                    () => InstallSnapshot.Write(snapshotPath, source, install.Commit, install.Branch),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }

    private string? CacheDirectory(SkillDefinition skill, string? branch)
    {
        if (skill.IsLan && !string.IsNullOrWhiteSpace(branch))
        {
            return _paths.LanCacheDirectory(skill.Host, skill.ResolvedInstallName, branch);
        }

        return RepoUrl.TryParse(skill.Repo, out var repo)
            ? _paths.RepoCacheDirectory(repo)
            : null;
    }

    private static string BranchKey(SkillDefinition skill, string? nasUser)
    {
        return skill.IsLan ? skill.SshUrl(nasUser ?? "") : skill.Repo;
    }
}
