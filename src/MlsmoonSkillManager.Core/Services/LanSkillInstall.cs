using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class LanSkillInstall
{
    private readonly AppPaths _paths;
    private readonly GitRemote _git;
    private readonly WorkspaceRepo _workspaceRepo;

    public LanSkillInstall(AppPaths paths, GitRemote git, WorkspaceRepo workspaceRepo)
    {
        _paths = paths;
        _git = git;
        _workspaceRepo = workspaceRepo;
    }

    public async Task RunAsync(
        SkillDefinition skill,
        string workspacePath,
        string? nasUser,
        string? branchName,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        var editor = UnityWorkspace.ReadEditorVersion(workspacePath);
        var branch = ResolveLanBranch(skill, editor, branchName)
                     ?? throw new InvalidOperationException($"{skill.DisplayName} 没有可用的局域网分支。");
        log?.Invoke(editor is not null
            ? $"Unity {editor} → 分支 {branch.Name}"
            : $"未读到 ProjectVersion，使用分支 {branch.Name}");
        if (SkillGit.IsForbiddenBranch(skill, editor, branch.Name))
        {
            throw new InvalidOperationException("Unity 6+ 不能使用 master 上的 URP 14，请用 urp-17.5。");
        }

        var url = skill.SshUrl(nasUser ?? "");
        var cache = _paths.LanCacheDirectory(skill.Host, skill.ResolvedInstallName, branch.Name);
        _paths.EnsureWritable();
        await _git.CloneOrUpdateAsync(url, cache, branch.Name, log, cancellationToken).ConfigureAwait(false);
        var source = SkillInstaller.ResolveSource(cache, skill.ResolvedSourcePath, skill.DisplayName, requireSkillMarkdown: false);
        var dest = SkillInstaller.ResolvePluginDestination(workspacePath, skill);
        SkillInstaller.WarnIfNotUnityProject(workspacePath, log);
        log?.Invoke($"安装 {skill.DisplayName} → {skill.ResolvedInstallPath}（含 .git）");
        await _workspaceRepo.SyncSkillAsync(
                source, dest, url, branch.Name, log, cancellationToken)
            .ConfigureAwait(false);
        var commit = await _git.ReadHeadCommitAsync(
                WorkspaceGit.HasRepo(dest) ? dest : cache, cancellationToken)
            .ConfigureAwait(false);
        var marker = SkillInstaller.CreateMarker(skill, url, commit, branch.Name);
        SkillInstaller.WriteMarker(dest, marker, ProjectCopy.MarkerFileName(skill));
        InstallSnapshot.Write(_paths.SnapshotPath(workspacePath, skill.Id, skill.ResolvedInstallPath), dest, commit, branch.Name);
        if (branch.Manifest.Count > 0)
        {
            UnityWorkspace.MergeManifest(workspacePath, branch.Manifest);
            log?.Invoke("已写入 Packages/manifest.json 的 file: 依赖。");
        }

        log?.Invoke("若项目里已有 LyShaders，不要重复接入 com.igp.render.extend，以免 ShaderName 冲突。");
    }

    private static LanGitBranch? ResolveLanBranch(SkillDefinition skill, string? editor, string? branchName)
    {
        if (!string.IsNullOrWhiteSpace(branchName))
        {
            return skill.Branches.FirstOrDefault(item =>
                       item.Name.Equals(branchName, StringComparison.OrdinalIgnoreCase))
                   ?? new LanGitBranch { Name = branchName.Trim() };
        }

        return UnityWorkspace.PickBranch(skill, editor);
    }
}
