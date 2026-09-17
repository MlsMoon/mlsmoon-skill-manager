using System.Text.Json;
using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class SkillInstaller
{
    public const string MarkerFileName = ".mlsmoon-skill.json";
    public const string PluginMarkerFileName = ".mlsmoon-plugin.json";

    private static readonly HashSet<string> SkipNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".github", ".vs", "bin", "obj", ".idea"
    };

    private static readonly HashSet<string> PluginSkipNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".github", ".vs", "bin", "obj", ".idea", "Skills~"
    };

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
            await InstallLanAsync(skill, workspacePath, nasUser, branch, log, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!RepoUrl.TryParse(skill.Repo, out var repo))
        {
            throw new InvalidOperationException($"{KindLabel(skill)} {skill.DisplayName} 的仓库地址无效。");
        }

        if (!skill.IsPlugin && roots.Count == 0)
        {
            throw new InvalidOperationException($"请至少选择一个安装目标（默认 {SkillRoots.DefaultRoot}）。");
        }

        _paths.EnsureWritable();
        var cache = _paths.RepoCacheDirectory(repo);
        await _gh.CloneOrUpdateAsync(repo, cache, log, branch, cancellationToken).ConfigureAwait(false);
        var source = ResolveSource(cache, skill.ResolvedSourcePath, skill.DisplayName, requireSkillMarkdown: !skill.IsPlugin);
        var commit = await _gh.ReadHeadCommitAsync(cache, cancellationToken).ConfigureAwait(false);
        var resolvedBranch = string.IsNullOrWhiteSpace(branch)
            ? await _gh.ReadCurrentBranchAsync(cache, cancellationToken).ConfigureAwait(false)
            : branch.Trim();
        var marker = CreateMarker(skill, repo.HttpsUrl, commit, resolvedBranch);

        if (skill.IsPlugin)
        {
            var dest = ResolvePluginDestination(workspacePath, skill);
            WarnIfNotUnityProject(workspacePath, log);
            log?.Invoke($"安装 Plugin {skill.DisplayName} → {skill.ResolvedInstallPath}（{resolvedBranch}）");
            CopySkill(source, dest, PluginSkipNames);
            WriteMarker(dest, marker, PluginMarkerFileName);
            WriteSnapshot(workspacePath, skill.Id, skill.ResolvedInstallPath, dest, commit, resolvedBranch);
            await InstallCompanionsAsync(skill, cache, repo, commit, resolvedBranch, workspacePath, roots, log, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        InstallSkillCopy(skill, source, repo, commit, resolvedBranch, workspacePath, roots, log);
    }

    public void Uninstall(SkillDefinition skill, string workspacePath, IReadOnlyList<string> roots, Action<string>? log = null)
    {
        if (skill.IsPlugin)
        {
            var dest = ResolvePluginDestination(workspacePath, skill);
            if (!Directory.Exists(dest))
            {
                return;
            }

            var markerPath = Path.Combine(dest, PluginMarkerFileName);
            if (!File.Exists(markerPath))
            {
                throw new InvalidOperationException(
                    $"{skill.ResolvedInstallPath} 没有本工具标记，未卸载以免误删本地 Plugin。");
            }

            log?.Invoke($"卸载 Plugin {skill.DisplayName} ← {skill.ResolvedInstallPath}");
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

    public static void CopySkill(string source, string dest, ISet<string>? extraSkip = null)
    {
        if (Directory.Exists(dest))
        {
            Directory.Delete(dest, true);
        }

        CopyDirectory(source, dest, extraSkip);
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

    public static string ResolvePluginDestination(string workspacePath, SkillDefinition plugin)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            throw new ArgumentException("工作区路径不能为空。", nameof(workspacePath));
        }

        var workspace = Path.GetFullPath(workspacePath);
        var relative = plugin.ResolvedInstallPath;
        if (string.IsNullOrWhiteSpace(relative)
            || Path.IsPathRooted(relative)
            || relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part is ".." or "."))
        {
            throw new InvalidOperationException($"Plugin {plugin.DisplayName} 的 installPath 无效: {plugin.InstallPath}");
        }

        var dest = Path.GetFullPath(Path.Combine(workspace, relative));
        var prefix = workspace.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        if (!dest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Plugin {plugin.DisplayName} 的安装路径必须位于工作区内。");
        }

        return dest;
    }

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
            CopySkill(source, dest);
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

    private static void WarnIfNotUnityProject(string workspacePath, Action<string>? log)
    {
        var assets = Path.Combine(workspacePath, "Assets");
        var projectSettings = Path.Combine(workspacePath, "ProjectSettings");
        if (Directory.Exists(assets) && Directory.Exists(projectSettings))
        {
            return;
        }

        log?.Invoke("当前工作区未见 Unity 的 Assets / ProjectSettings，仍按 Plugin 路径安装。");
    }

    private async Task InstallLanAsync(
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
        if (editor is not null)
        {
            log?.Invoke($"Unity {editor} → 分支 {branch.Name}");
        }
        else
        {
            log?.Invoke($"未读到 ProjectVersion，使用分支 {branch.Name}");
        }

        if (SkillGit.IsForbiddenBranch(skill, editor, branch.Name))
        {
            throw new InvalidOperationException("Unity 6+ 不能使用 master 上的 URP 14，请用 urp-17.5。");
        }

        var url = skill.SshUrl(nasUser ?? "");
        var cache = _paths.LanCacheDirectory(skill.Host, skill.ResolvedInstallName, branch.Name);
        _paths.EnsureWritable();
        await _git.CloneOrUpdateAsync(url, cache, branch.Name, log, cancellationToken).ConfigureAwait(false);
        var source = ResolveSource(cache, skill.ResolvedSourcePath, skill.DisplayName, requireSkillMarkdown: false);
        var commit = await _git.ReadHeadCommitAsync(cache, cancellationToken).ConfigureAwait(false);
        var dest = ResolvePluginDestination(workspacePath, skill);
        WarnIfNotUnityProject(workspacePath, log);
        log?.Invoke($"安装 {skill.DisplayName} → {skill.ResolvedInstallPath}（不含 .git）");
        CopySkill(source, dest, PluginSkipNames);
        WriteMarker(dest, CreateMarker(skill, url, commit, branch.Name), PluginMarkerFileName);
        WriteSnapshot(workspacePath, skill.Id, skill.ResolvedInstallPath, dest, commit, branch.Name);
        if (branch.Manifest.Count > 0)
        {
            UnityWorkspace.MergeManifest(workspacePath, branch.Manifest);
            log?.Invoke("已写入 Packages/manifest.json 的 file: 依赖。");
        }

        log?.Invoke("若项目里已有 LyShaders，不要重复接入 com.igp.render.extend，以免 ShaderName 冲突。");
    }

    private static InstallMarker CreateMarker(SkillDefinition skill, string repo, string commit, string branch)
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

    private void WriteSnapshot(string workspacePath, string skillId, string root, string dest, string commit, string branch)
    {
        _paths.EnsureWritable();
        InstallSnapshot.Write(_paths.SnapshotPath(workspacePath, skillId, root), dest, commit, branch);
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

    private static string KindLabel(SkillDefinition skill) =>
        skill.IsPlugin ? "Plugin" : skill.IsCompanion ? "随附 Skill" : "Skill";

    private static void CopyDirectory(string source, string dest, ISet<string>? extraSkip)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.GetFiles(source))
        {
            var name = Path.GetFileName(file);
            if (ShouldSkip(name, extraSkip))
            {
                continue;
            }

            File.Copy(file, Path.Combine(dest, name), true);
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            var name = Path.GetFileName(dir);
            if (ShouldSkip(name, extraSkip))
            {
                continue;
            }

            CopyDirectory(dir, Path.Combine(dest, name), extraSkip);
        }
    }

    private static bool ShouldSkip(string name, ISet<string>? extraSkip)
    {
        return SkipNames.Contains(name) || extraSkip is not null && extraSkip.Contains(name);
    }
}
