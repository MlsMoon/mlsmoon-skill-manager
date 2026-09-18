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
        foreach (var parent in skills.Where(item => item.IsProjectCopy).ToList())
        {
            var config = ReadConfig(parent, workspacePath, paths);
            if (config is null || !config.IsRouting || !OwnsRouting(parent, config))
            {
                continue;
            }

            var routing = MlsmoonSkillConfig.ToRoutingCompanion(parent, config);
            var existing = skills.FirstOrDefault(item =>
                item.Id.Equals(routing.Id, StringComparison.OrdinalIgnoreCase));
            parent.CompanionSkills.Clear();
            if (existing is not null)
            {
                existing.IsRouting = true;
                existing.Kind = ToolKind.Companion;
                existing.ParentPluginId = parent.Id;
                existing.ParentPluginName = parent.DisplayName;
                existing.InstallName = string.IsNullOrWhiteSpace(existing.InstallName) ? routing.Id : existing.InstallName;
                existing.SourcePath = routing.SourcePath;
                if (string.IsNullOrWhiteSpace(existing.Repo))
                {
                    existing.Repo = parent.Repo;
                }

                parent.CompanionSkills.Add(existing);
            }
            else
            {
                parent.CompanionSkills.Add(routing);
                result.Add.Add(routing);
            }

            foreach (var companion in skills.Where(item =>
                         item.IsCompanion
                         && item.ParentPluginId.Equals(parent.Id, StringComparison.OrdinalIgnoreCase)
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

    private static bool OwnsRouting(SkillDefinition parent, MlsmoonSkillFile config) =>
        string.IsNullOrWhiteSpace(config.ParentId)
        || config.ParentId.Equals(parent.Id, StringComparison.OrdinalIgnoreCase);
}
