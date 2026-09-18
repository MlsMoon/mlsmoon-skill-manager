using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class SkillGit
{
    private readonly SkillGitRemote _tips;
    private readonly GitCompare _compare;
    private readonly WorkspaceGit _workspaceGit;
    private readonly WorkspaceRepo _workspaceRepo;
    private readonly InstallTreeAlign _align;

    public SkillGit(AppPaths paths, GhCli gh, GitRemote git, IProcessRunner? runner = null)
    {
        var process = runner ?? new ProcessRunner();
        _tips = new SkillGitRemote(paths, gh, git);
        _compare = new GitCompare(process);
        _workspaceGit = new WorkspaceGit(process);
        _workspaceRepo = new WorkspaceRepo(process);
        _align = new InstallTreeAlign(paths, git, _workspaceGit);
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

    public void ClearCaches() => _tips.Clear();

    public Task<IReadOnlyList<string>> ListBranchesAsync(
        SkillDefinition skill,
        string? nasUser,
        bool canReachRemote,
        CancellationToken cancellationToken = default) =>
        _tips.ListBranchesAsync(skill, nasUser, canReachRemote, cancellationToken);

    public Task<string> RemoteCommitAsync(
        SkillDefinition skill,
        string branch,
        string? nasUser,
        CancellationToken cancellationToken = default) =>
        _tips.RemoteCommitAsync(skill, branch, nasUser, cancellationToken);

    public void RememberRemote(SkillDefinition skill, string? nasUser, string branch, string commit) =>
        _tips.RememberRemote(skill, nasUser, branch, commit);

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
        var branches = await _tips.ListBranchesAsync(skill, nasUser, canReachRemote, cancellationToken)
            .ConfigureAwait(false);
        var target = ResolveTarget(skill, editorVersion, selectedBranch, installedBranch, ref branches);
        var remoteCommit = "";
        if (canReachRemote && !string.IsNullOrWhiteSpace(target))
        {
            remoteCommit = await _tips.RemoteCommitAsync(skill, target, nasUser, cancellationToken)
                .ConfigureAwait(false);
        }

        if (installed && canReachRemote)
        {
            await PrepareReposAsync(skill, copies, target, nasUser, cancellationToken).ConfigureAwait(false);
        }

        if (first is not null && WorkspaceGit.HasRepo(first.Path))
        {
            installedCommit = await FillGitValueAsync(
                    "", () => _workspaceGit.ReadHeadCommitAsync(first.Path, cancellationToken))
                .ConfigureAwait(false);
            installedBranch = await FillGitValueAsync(
                    "", () => _workspaceGit.ReadCurrentBranchAsync(first.Path, cancellationToken))
                .ConfigureAwait(false);
        }

        var cache = _tips.CacheDirectory(skill, string.IsNullOrWhiteSpace(target) ? installedBranch : target);
        var align = await _align.InspectAsync(
                skill, workspacePath, copies, remoteCommit, target, cache, cancellationToken)
            .ConfigureAwait(false);
        var compare = await ResolveCompareAsync(
                skill, installedCommit, remoteCommit, cache, align, cancellationToken)
            .ConfigureAwait(false);
        var hasLocal = align.SnapshotChanges.Count > 0;
        if (align.MatchesRemote)
        {
            hasLocal = false;
            if (!string.IsNullOrWhiteSpace(remoteCommit))
            {
                installedCommit = remoteCommit;
            }

            if (!string.IsNullOrWhiteSpace(target))
            {
                installedBranch = target;
            }
        }

        var branchDiffers = installed
                            && !string.IsNullOrWhiteSpace(installedBranch)
                            && !string.IsNullOrWhiteSpace(target)
                            && !installedBranch.Equals(target, StringComparison.OrdinalIgnoreCase);
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
            Changes = align.SnapshotChanges,
            Forbidden = forbidden,
            Warning = forbidden ? "Unity 6 不能使用 master 上的 URP 14，请改选 urp-17.5。" : "",
            Message = SkillGitStatus.Describe(
                state,
                compare,
                target,
                installedBranch,
                installedCommit,
                remoteCommit,
                align.SnapshotChanges.Count,
                canReachRemote,
                remoteCommit.Length > 0)
        };
    }

    private async Task<CommitCompare> ResolveCompareAsync(
        SkillDefinition skill,
        string installedCommit,
        string remoteCommit,
        string? cache,
        AlignReport align,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(remoteCommit))
        {
            return CommitCompare.Unknown;
        }

        if (align.MatchesRemote)
        {
            return CommitCompare.Same;
        }

        if (string.IsNullOrWhiteSpace(installedCommit) && align.ComparedAll)
        {
            return CommitCompare.Behind();
        }

        return await _compare.CompareAsync(skill, installedCommit, remoteCommit, cache, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task PrepareReposAsync(
        SkillDefinition skill,
        IReadOnlyList<RootInstallStatus> copies,
        string branch,
        string? nasUser,
        CancellationToken cancellationToken)
    {
        if (!WorkspaceRepo.CanAttach(skill) || string.IsNullOrWhiteSpace(branch))
        {
            return;
        }

        var origin = WorkspaceRepo.OriginUrl(skill, nasUser);
        if (string.IsNullOrWhiteSpace(origin))
        {
            return;
        }

        foreach (var copy in copies)
        {
            try
            {
                await _workspaceRepo.EnsureAttachedAsync(copy.Path, origin, branch, null, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    private static async Task<string> FillGitValueAsync(string current, Func<Task<string>> read)
    {
        if (!string.IsNullOrWhiteSpace(current))
        {
            return current;
        }

        return await read().ConfigureAwait(false);
    }

    private static string ResolveTarget(
        SkillDefinition skill,
        string? editorVersion,
        string? selectedBranch,
        string installedBranch,
        ref IReadOnlyList<string> branches)
    {
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

        return target;
    }
}
