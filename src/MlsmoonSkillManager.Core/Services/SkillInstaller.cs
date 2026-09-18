using System.Text.Json;
using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class SkillInstaller
{
    public const string MarkerFileName = ".mlsmoon-skill.json";
    public const string PluginMarkerFileName = ProjectCopy.PluginMarker;
    public const string PackageMarkerFileName = ProjectCopy.PackageMarker;

    private readonly AppPaths _paths;
    private readonly GhCli _gh;
    private readonly GitRemote _git;

    public SkillInstaller(AppPaths paths, GhCli gh, IProcessRunner? runner = null)
    {
        _paths = paths;
        _gh = gh;
        _git = new GitRemote(runner ?? new ProcessRunner());
    }

    public async Task InstallAsync(
        SkillDefinition skill,
        string workspacePath,
        IReadOnlyList<string> roots,
        Action<string>? log = null,
        string? nasUser = null,
        string? branch = null,
        CancellationToken cancellationToken = default)
    {
        if (skill.IsLan)
        {
            await new LanSkillInstall(_paths, _git)
                .RunAsync(skill, workspacePath, nasUser, branch, log, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (!RepoUrl.TryParse(skill.Repo, out var repo))
        {
            throw new InvalidOperationException($"{skill.KindLabel} {skill.DisplayName} 的仓库地址无效。");
        }

        if (!skill.IsProjectCopy && roots.Count == 0)
        {
            throw new InvalidOperationException($"请至少选择一个安装目标（默认 {SkillRoots.DefaultRoot}）。");
        }

        _paths.EnsureWritable();
        var cache = _paths.RepoCacheDirectory(repo);
        await _gh.CloneOrUpdateAsync(repo, cache, log, branch, cancellationToken).ConfigureAwait(false);
        var source = ResolveSource(cache, skill.ResolvedSourcePath, skill.DisplayName, requireSkillMarkdown: !skill.IsProjectCopy);
        var commit = await _gh.ReadHeadCommitAsync(cache, cancellationToken).ConfigureAwait(false);
        var resolvedBranch = string.IsNullOrWhiteSpace(branch)
            ? await _gh.ReadCurrentBranchAsync(cache, cancellationToken).ConfigureAwait(false)
            : branch.Trim();
        var marker = CreateMarker(skill, repo.HttpsUrl, commit, resolvedBranch);

        if (skill.IsProjectCopy)
        {
            var dest = ResolvePluginDestination(workspacePath, skill);
            WarnIfNotUnityProject(workspacePath, log);
            log?.Invoke($"安装 {skill.KindLabel} {skill.DisplayName} → {skill.ResolvedInstallPath}（{resolvedBranch}）");
            SkillCopy.Replace(source, dest, ProjectCopy.SkipNames);
            WriteMarker(dest, marker, ProjectCopy.MarkerFileName(skill));
            WriteSnapshot(workspacePath, skill.Id, skill.ResolvedInstallPath, dest, commit, resolvedBranch);
            await InstallCompanionsAsync(skill, cache, repo, commit, resolvedBranch, workspacePath, roots, log, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        InstallSkillCopy(skill, source, repo, commit, resolvedBranch, workspacePath, roots, log);
    }

    public void Uninstall(SkillDefinition skill, string workspacePath, IReadOnlyList<string> roots, Action<string>? log = null)
    {
        if (skill.IsProjectCopy)
        {
            var dest = ResolvePluginDestination(workspacePath, skill);
            if (!Directory.Exists(dest))
            {
                return;
            }

            if (ProjectCopy.FindMarker(dest) is null)
            {
                throw new InvalidOperationException(
                    $"{skill.ResolvedInstallPath} 没有本工具标记，未卸载以免误删本地 {skill.KindLabel}。");
            }

            log?.Invoke($"卸载 {skill.KindLabel} {skill.DisplayName} ← {skill.ResolvedInstallPath}");
            Directory.Delete(dest, true);
            InstallSnapshot.Delete(_paths.SnapshotPath(workspacePath, skill.Id, skill.ResolvedInstallPath));
            if (skill.IsLan)
            {
                UnityWorkspace.RemoveManifestPrefix(workspacePath, "file:" + skill.ResolvedInstallName + "/");
            }

            foreach (var companion in skill.CompanionSkills)
            {
                UninstallSkillCopy(companion, workspacePath, SkillRoots.Normalize(roots), log);
            }

            return;
        }

        UninstallSkillCopy(skill, workspacePath, roots, log);
    }

    public static InstallMarker? ReadMarker(string markerPath)
    {
        if (!File.Exists(markerPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<InstallMarker>(File.ReadAllText(markerPath), JsonUtil.Options);
    }

    public static void WriteMarker(string dest, InstallMarker marker, string? fileName = null)
    {
        Directory.CreateDirectory(dest);
        File.WriteAllText(
            Path.Combine(dest, fileName ?? MarkerFileName),
            JsonSerializer.Serialize(marker, JsonUtil.Options));
    }

    public static void CopySkill(string source, string dest, ISet<string>? extraSkip = null) =>
        SkillCopy.Replace(source, dest, extraSkip);

    public static void WarnIfNotUnityProject(string workspacePath, Action<string>? log)
    {
        var assets = Path.Combine(workspacePath, "Assets");
        var projectSettings = Path.Combine(workspacePath, "ProjectSettings");
        if (Directory.Exists(assets) && Directory.Exists(projectSettings))
        {
            return;
        }

        log?.Invoke("当前工作区未见 Unity 的 Assets / ProjectSettings，仍按项目路径安装。");
    }

    public static InstallMarker CreateMarker(SkillDefinition skill, string repo, string commit, string branch)
    {
        return new InstallMarker
        {
            Id = skill.Id,
            Repo = repo,
            SourcePath = skill.ResolvedSourcePath,
            InstalledAtUtc = DateTime.UtcNow.ToString("o"),
            Commit = commit,
            Branch = branch,
            ManagerVersion = typeof(SkillInstaller).Assembly.GetName().Version?.ToString() ?? "",
            ParentPluginId = skill.ParentPluginId
        };
    }

    public static string ResolveSource(
        string cacheDirectory,
        string sourcePath,
        string skillName,
        bool requireSkillMarkdown = true)
    {
        var source = sourcePath is "." or "./"
            ? cacheDirectory
            : Path.GetFullPath(Path.Combine(cacheDirectory, sourcePath));

        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException($"{skillName} 的 sourcePath 不存在: {sourcePath}");
        }

        if (requireSkillMarkdown && !File.Exists(Path.Combine(source, "SKILL.md")))
        {
            throw new InvalidOperationException($"Skill {skillName} 缺少 SKILL.md: {sourcePath}");
        }

        return source;
    }

    public static string ResolvePluginDestination(string workspacePath, SkillDefinition plugin) =>
        ProjectCopy.ResolveDestination(workspacePath, plugin);

    private async Task InstallCompanionsAsync(
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

            var source = ResolveSource(cache, companion.ResolvedSourcePath, companion.DisplayName);
            InstallSkillCopy(companion, source, repo, commit, branch, workspacePath, skillRoots, log);
        }
    }

    private void InstallSkillCopy(
        SkillDefinition skill,
        string source,
        GitHubRepoRef repo,
        string commit,
        string branch,
        string workspacePath,
        IReadOnlyList<string> roots,
        Action<string>? log)
    {
        var targets = SkillRoots.Normalize(roots);
        if (targets.Count == 0)
        {
            throw new InvalidOperationException($"请至少选择一个安装目标（默认 {SkillRoots.DefaultRoot}）。");
        }

        var marker = CreateMarker(skill, repo.HttpsUrl, commit, branch);
        foreach (var root in targets)
        {
            var dest = WorkspaceScanner.SkillInstallPath(workspacePath, root, skill.ResolvedInstallName);
            var label = skill.IsCompanion ? "随附 Skill" : "Skill";
            log?.Invoke($"安装 {label} {skill.DisplayName} → {root}/skills/{skill.ResolvedInstallName}");
            SkillCopy.Replace(source, dest);
            WriteMarker(dest, marker);
            WriteSnapshot(workspacePath, skill.Id, root, dest, commit, branch);
        }
    }

    private void UninstallSkillCopy(
        SkillDefinition skill,
        string workspacePath,
        IReadOnlyList<string> roots,
        Action<string>? log)
    {
        foreach (var root in SkillRoots.Expand(roots))
        {
            var dest = WorkspaceScanner.SkillInstallPath(workspacePath, root, skill.ResolvedInstallName);
            if (!Directory.Exists(dest))
            {
                continue;
            }

            var markerPath = Path.Combine(dest, MarkerFileName);
            if (!File.Exists(markerPath))
            {
                throw new InvalidOperationException(
                    $"{root}/skills/{skill.ResolvedInstallName} 没有本工具标记，未卸载以免误删本地 Skill。");
            }

            var label = skill.IsCompanion ? "随附 Skill" : "Skill";
            log?.Invoke($"卸载 {label} {skill.DisplayName} ← {root}/skills/{skill.ResolvedInstallName}");
            Directory.Delete(dest, true);
            InstallSnapshot.Delete(_paths.SnapshotPath(workspacePath, skill.Id, root));
        }
    }

    private void WriteSnapshot(string workspacePath, string skillId, string root, string dest, string commit, string branch)
    {
        _paths.EnsureWritable();
        InstallSnapshot.Write(_paths.SnapshotPath(workspacePath, skillId, root), dest, commit, branch);
    }
}
