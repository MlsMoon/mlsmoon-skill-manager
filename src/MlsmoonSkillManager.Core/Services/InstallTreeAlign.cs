using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class AlignReport
{
    public bool MatchesRemote { get; init; }
    public bool ComparedAll { get; init; }
    public bool HasUserGit { get; init; }
    public IReadOnlyList<GitChange> SnapshotChanges { get; init; } = [];
}

public sealed class InstallTreeAlign
{
    private readonly AppPaths _paths;
    private readonly GitRemote _git;
    private readonly WorkspaceGit _workspaceGit;

    public InstallTreeAlign(AppPaths paths, GitRemote git, WorkspaceGit workspaceGit)
    {
        _paths = paths;
        _git = git;
        _workspaceGit = workspaceGit;
    }

    public async Task<AlignReport> InspectAsync(
        SkillDefinition skill,
        string workspacePath,
        IReadOnlyList<RootInstallStatus> copies,
        string remoteCommit,
        string targetBranch,
        string? cacheDirectory,
        CancellationToken cancellationToken = default)
    {
        var hasUserGit = copies.Any(item => item.HasGit);
        var changes = new List<GitChange>();
        if (copies.Count == 0 || string.IsNullOrWhiteSpace(workspacePath))
        {
            return new AlignReport { HasUserGit = hasUserGit, SnapshotChanges = changes };
        }

        var compared = 0;
        var matched = 0;
        foreach (var item in copies)
        {
            var snap = _paths.SnapshotPath(workspacePath, skill.Id, item.Root);
            await TrySeedSnapshotAsync(skill, snap, item, cacheDirectory, cancellationToken).ConfigureAwait(false);
            var found = InstallSnapshot.Read(snap) is null
                ? []
                : await Task.Run(() => InstallSnapshot.Diff(snap, item.Path), cancellationToken)
                    .ConfigureAwait(false);
            var match = await TryMatchRemoteAsync(skill, item, remoteCommit, cacheDirectory, cancellationToken)
                .ConfigureAwait(false);
            if (match is true)
            {
                compared++;
                matched++;
                found = [];
                TryWriteAlignedSnapshot(snap, item.Path, remoteCommit, targetBranch);
            }
            else if (match is false)
            {
                compared++;
            }

            AppendChanges(changes, found, item.Root, copies.Count == 1);
        }

        return new AlignReport
        {
            MatchesRemote = compared == copies.Count
                            && matched == copies.Count
                            && !string.IsNullOrWhiteSpace(remoteCommit),
            ComparedAll = compared == copies.Count,
            HasUserGit = hasUserGit,
            SnapshotChanges = changes
        };
    }

    private async Task<bool?> TryMatchRemoteAsync(
        SkillDefinition skill,
        RootInstallStatus install,
        string remoteCommit,
        string? cacheDirectory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(remoteCommit) || !Directory.Exists(install.Path))
        {
            return null;
        }

        var cacheMatch = await TryMatchCacheAsync(
                skill, install.Path, remoteCommit, cacheDirectory, cancellationToken)
            .ConfigureAwait(false);
        if (cacheMatch is not null)
        {
            return cacheMatch;
        }

        if (!WorkspaceGit.HasRepo(install.Path)
            || !await _workspaceGit.HasCommitAsync(install.Path, remoteCommit, cancellationToken)
                .ConfigureAwait(false))
        {
            return null;
        }

        return await _workspaceGit.MatchesCommitAsync(install.Path, remoteCommit, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool?> TryMatchCacheAsync(
        SkillDefinition skill,
        string installPath,
        string remoteCommit,
        string? cacheDirectory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cacheDirectory)
            || !Directory.Exists(Path.Combine(cacheDirectory, ".git")))
        {
            return null;
        }

        var head = await _git.ReadHeadCommitAsync(cacheDirectory, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(head)
            || !head.Equals(remoteCommit, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var source = SkillInstaller.ResolveSource(
                cacheDirectory,
                skill.ResolvedSourcePath,
                skill.DisplayName,
                requireSkillMarkdown: false);
            return await Task.Run(
                    () => InstallSnapshot.Diff(
                        InstallSnapshot.HashTree(source),
                        InstallSnapshot.HashTree(installPath)).Count == 0,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task TrySeedSnapshotAsync(
        SkillDefinition skill,
        string snapshotPath,
        RootInstallStatus install,
        string? cacheDirectory,
        CancellationToken cancellationToken)
    {
        if (InstallSnapshot.Read(snapshotPath) is not null || !Directory.Exists(install.Path))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(cacheDirectory)
            || !Directory.Exists(Path.Combine(cacheDirectory, ".git")))
        {
            return;
        }

        var head = await _git.ReadHeadCommitAsync(cacheDirectory, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(install.Commit)
            || !head.Equals(install.Commit, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var source = SkillInstaller.ResolveSource(
                cacheDirectory,
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

    private void TryWriteAlignedSnapshot(string snapshotPath, string directory, string commit, string branch)
    {
        try
        {
            _paths.EnsureWritable();
            InstallSnapshot.Write(snapshotPath, directory, commit, branch);
        }
        catch (Exception)
        {
        }
    }

    private static void AppendChanges(
        List<GitChange> changes,
        IReadOnlyList<GitChange> found,
        string root,
        bool single)
    {
        if (single)
        {
            changes.AddRange(found);
            return;
        }

        changes.AddRange(found.Select(change => new GitChange
        {
            Kind = change.Kind,
            Path = root + ": " + change.Path
        }));
    }
}
