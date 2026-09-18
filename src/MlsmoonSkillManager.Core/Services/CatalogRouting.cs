using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class CatalogRoutingBindResult
{
    public List<SkillDefinition> Add { get; } = [];
    public List<string> RemoveIds { get; } = [];
}

public static class CatalogRouting
{
    public static CatalogRoutingBindResult Bind(
        IReadOnlyList<SkillDefinition> skills,
        string? workspacePath,
        AppPaths paths)
    {
        var result = new CatalogRoutingBindResult();
        foreach (var plugin in skills.Where(item => item.IsProjectCopy).ToList())
        {
            var config = ReadConfig(plugin, workspacePath, paths);
            if (config is null || !config.IsRouting)
            {
                continue;
            }

            var routing = MlsmoonSkillConfig.ToRoutingCompanion(plugin, config);
            var existing = skills.FirstOrDefault(item =>
                item.Id.Equals(routing.Id, StringComparison.OrdinalIgnoreCase));
            plugin.CompanionSkills.Clear();
            if (existing is not null)
            {
                existing.IsRouting = true;
                existing.Kind = ToolKind.Companion;
                existing.ParentPluginId = plugin.Id;
                existing.ParentPluginName = plugin.DisplayName;
                existing.InstallName = string.IsNullOrWhiteSpace(existing.InstallName) ? routing.Id : existing.InstallName;
                if (string.IsNullOrWhiteSpace(existing.Repo))
                {
                    existing.Repo = plugin.Repo;
                }

                plugin.CompanionSkills.Add(existing);
            }
            else
            {
                plugin.CompanionSkills.Add(routing);
                result.Add.Add(routing);
            }

            foreach (var companion in skills.Where(item =>
                         item.IsCompanion
                         && item.ParentPluginId.Equals(plugin.Id, StringComparison.OrdinalIgnoreCase)
                         && !item.Id.Equals(routing.Id, StringComparison.OrdinalIgnoreCase)))
            {
                result.RemoveIds.Add(companion.Id);
            }
        }

        return result;
    }

    public static MlsmoonSkillFile? ReadConfig(SkillDefinition plugin, string? workspacePath, AppPaths paths)
    {
        if (!string.IsNullOrWhiteSpace(workspacePath) && Directory.Exists(workspacePath))
        {
            try
            {
                var dest = ProjectCopy.ResolveDestination(workspacePath, plugin);
                var installed = MlsmoonSkillConfig.Read(dest);
                if (installed is not null)
                {
                    return installed;
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        if (RepoUrl.TryParse(plugin.Repo, out var repo))
        {
            var cached = MlsmoonSkillConfig.Read(paths.RepoCacheDirectory(repo));
            if (cached is not null)
            {
                return cached;
            }
        }

        return null;
    }
}
