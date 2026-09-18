using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class SkillCopyInstall
{
    private readonly AppPaths _paths;
    private readonly GhCli _gh;
    private readonly WorkspaceRepo _workspaceRepo;

    public SkillCopyInstall(AppPaths paths, GhCli gh, WorkspaceRepo workspaceRepo)
    {
        _paths = paths;
        _gh = gh;
        _workspaceRepo = workspaceRepo;
    }

    public async Task InstallAsync(
        SkillDefinition skill,
        string source,
        GitHubRepoRef repo,
        string commit,
        string branch,
        string workspacePath,
        IReadOnlyList<string> roots,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        var targets = SkillRoots.Normalize(roots);
        if (targets.Count == 0)
        {
            throw new InvalidOperationException($"请至少选择一个安装目标（默认 {SkillRoots.DefaultRoot}）。");
        }

        var marker = SkillInstaller.CreateMarker(skill, repo.HttpsUrl, commit, branch);
        foreach (var root in targets)
        {
            var dest = MlsmoonSkillConfig.ResolveInstallDirectory(workspacePath, root, skill.Id);
            var label = skill.IsRouting ? "路由 Skill" : skill.IsCompanion ? "随附 Skill" : "Skill";
            log?.Invoke($"安装 {label} {skill.DisplayName} → {root}/skills/{Path.GetFileName(dest)}");
            if (WorkspaceRepo.CanAttach(skill))
            {
                await _workspaceRepo.SyncSkillAsync(source, dest, repo.HttpsUrl, branch, log, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                SkillCopy.Replace(source, dest);
            }

            SkillInstaller.WriteMarker(dest, marker);
            MlsmoonSkillConfig.WriteIdentity(dest, skill);
            WriteSnapshot(workspacePath, skill.Id, root, dest, commit, branch);
        }
    }

    public async Task InstallCompanionsAsync(
        SkillDefinition plugin,
        string pluginCache,
        GitHubRepoRef pluginRepo,
        string commit,
        string branch,
        string workspacePath,
        IReadOnlyList<string> roots,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        if (plugin.CompanionSkills.Count == 0)
        {
            return;
        }

        var skillRoots = SkillRoots.Normalize(roots);
        log?.Invoke($"一并安装随附 Skill → {string.Join(", ", skillRoots.Select(root => root + "/skills"))}");
        foreach (var companion in plugin.CompanionSkills)
        {
            if (!RepoUrl.TryParse(companion.Repo, out var repo))
            {
                throw new InvalidOperationException($"随附 Skill {companion.DisplayName} 的仓库地址无效。");
            }

            var cache = repo.HttpsUrl.Equals(pluginRepo.HttpsUrl, StringComparison.OrdinalIgnoreCase)
                ? pluginCache
                : _paths.RepoCacheDirectory(repo);
            if (!ReferenceEquals(cache, pluginCache))
            {
                await _gh.CloneOrUpdateAsync(repo, cache, log, branch, cancellationToken).ConfigureAwait(false);
                commit = await _gh.ReadHeadCommitAsync(cache, cancellationToken).ConfigureAwait(false);
            }

            var source = SkillInstaller.ResolveSource(cache, companion.ResolvedSourcePath, companion.DisplayName);
            await InstallAsync(
                    companion, source, repo, commit, branch, workspacePath, skillRoots, log, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private void WriteSnapshot(string workspacePath, string skillId, string root, string dest, string commit, string branch)
    {
        _paths.EnsureWritable();
        InstallSnapshot.Write(_paths.SnapshotPath(workspacePath, skillId, root), dest, commit, branch);
    }
}
