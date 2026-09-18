using System.Text;
using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public static class RoutingSkillInstall
{
    public static void Ensure(
        SkillDefinition plugin,
        MlsmoonSkillFile config,
        string workspacePath,
        IReadOnlyList<string> roots,
        InstallMarker pluginMarker,
        Action<string>? log)
    {
        var companion = MlsmoonSkillConfig.ToRoutingCompanion(plugin, config);
        var pluginDest = ProjectCopy.ResolveDestination(workspacePath, plugin);
        var source = RoutingSkillSource.PluginSourceDirectory(pluginDest, companion.Id);
        var hasSource = RoutingSkillSource.Compare(source, "").SourceExists;
        foreach (var root in SkillRoots.Normalize(roots))
        {
            var dest = MlsmoonSkillConfig.ResolveInstallDirectory(workspacePath, root, companion.Id);
            var existed = Directory.Exists(dest);
            Directory.CreateDirectory(dest);
            var skillMd = Path.Combine(dest, "SKILL.md");
            if (hasSource)
            {
                RoutingSkillSource.CopyOverlay(source, dest);
                log?.Invoke($"从源写入路由 Skill {companion.DisplayName} → {root}/skills/{Path.GetFileName(dest)}");
            }
            else if (!File.Exists(skillMd))
            {
                File.WriteAllText(skillMd, Render(plugin, config, companion));
                log?.Invoke($"写入路由 Skill {companion.DisplayName} → {root}/skills/{Path.GetFileName(dest)}");
            }

            MlsmoonSkillConfig.Write(dest, MlsmoonSkillConfig.IdentityFrom(companion));
            var markerPath = Path.Combine(dest, SkillInstaller.MarkerFileName);
            if (!existed || File.Exists(markerPath))
            {
                SkillInstaller.WriteMarker(
                    dest,
                    SkillInstaller.CreateMarker(companion, pluginMarker.Repo, pluginMarker.Commit, pluginMarker.Branch));
            }
        }
    }

    public static void UninstallMarkedChildren(
        SkillDefinition plugin,
        string keepId,
        string workspacePath,
        IReadOnlyList<string> roots,
        Action<string>? log)
    {
        foreach (var disk in SkillRoots.Expand(roots))
        {
            var folder = WorkspaceScanner.SkillsFolder(workspacePath, disk);
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var dir in Directory.GetDirectories(folder))
            {
                var marker = SkillInstaller.ReadMarker(Path.Combine(dir, SkillInstaller.MarkerFileName));
                if (marker is null
                    || !marker.ParentPluginId.Equals(plugin.Id, StringComparison.OrdinalIgnoreCase)
                    || marker.Id.Equals(keepId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                log?.Invoke($"卸载随附 Skill {marker.Id} ← {disk}/skills/{Path.GetFileName(dir)}");
                Directory.Delete(dir, true);
            }
        }
    }

    public static string Render(SkillDefinition plugin, MlsmoonSkillFile config, SkillDefinition companion)
    {
        var pluginPath = plugin.ResolvedInstallPath.Replace('\\', '/');
        var body = new StringBuilder();
        body.AppendLine("---");
        body.AppendLine($"name: {companion.Id}");
        body.AppendLine($"description: {companion.Description}");
        body.AppendLine("---");
        body.AppendLine();
        body.AppendLine($"# {plugin.DisplayName}");
        body.AppendLine();
        body.AppendLine("本文件由 Moon Game Dev Tool Manager 根据插件 `.mlsmoon` 生成。不要把项目专属约定写进这里。");
        body.AppendLine();
        body.AppendLine($"插件目录：`{pluginPath}/`");
        body.AppendLine();
        body.AppendLine("## 先读哪份");
        body.AppendLine();
        body.AppendLine("| 改什么 | 读 |");
        body.AppendLine("|---|---|");
        foreach (var route in config.Routes ?? [])
        {
            if (string.IsNullOrWhiteSpace(route.Path))
            {
                continue;
            }

            var title = string.IsNullOrWhiteSpace(route.Title) ? route.Path : route.Title;
            var relative = route.Path.Replace('\\', '/').TrimStart('/');
            body.AppendLine($"| {title} | `{pluginPath}/{relative}` |");
        }

        return body.ToString();
    }
}
