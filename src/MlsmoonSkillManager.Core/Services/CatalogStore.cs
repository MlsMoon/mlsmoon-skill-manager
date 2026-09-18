using System.Text.Json;
using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class CatalogStore
{
    public CatalogStore(AppPaths paths)
    {
        Paths = paths;
    }

    public AppPaths Paths { get; }

    public IReadOnlyList<string> SearchCatalogFiles()
    {
        var files = new List<string>();
        AddIfExists(files, Paths.BundledCatalogPath);
        AddIfExists(files, FindRepoCatalogNear(Directory.GetCurrentDirectory()));
        AddIfExists(files, FindRepoCatalogNear(Paths.AppDirectory));
        return files;
    }

    public SkillCatalogFile Load()
    {
        SkillCatalogFile? catalog = null;
        foreach (var file in SearchCatalogFiles())
        {
            catalog = ReadCatalog(file);
            if (catalog.Skills.Count > 0 || catalog.Plugins.Count > 0 || catalog.Packages.Count > 0)
            {
                break;
            }
        }

        catalog ??= new SkillCatalogFile();
        Merge(catalog, Paths.BundledOverridePath);
        if (!string.IsNullOrWhiteSpace(Paths.RepoOverridePath)
            && !string.Equals(Paths.RepoOverridePath, Paths.BundledOverridePath, StringComparison.OrdinalIgnoreCase))
        {
            Merge(catalog, Paths.RepoOverridePath);
        }

        Merge(catalog, Paths.UserOverridePath);
        return Normalize(catalog);
    }

    public static SkillCatalogFile Normalize(SkillCatalogFile catalog)
    {
        var map = new Dictionary<string, SkillDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var skill in catalog.Skills)
        {
            Ingest(map, skill, forcedKind: null);
        }

        foreach (var plugin in catalog.Plugins)
        {
            Ingest(map, plugin, ToolKind.Plugin);
            var stored = map[plugin.Id];
            stored.CompanionSkills = AttachCompanions(map, stored);
        }

        foreach (var package in catalog.Packages)
        {
            Ingest(map, package, ToolKind.Package);
            var stored = map[package.Id];
            stored.CompanionSkills = AttachCompanions(map, stored);
        }

        catalog.Plugins = [];
        catalog.Packages = [];
        catalog.Skills = [.. CatalogOrder.Sort(map.Values, skill => skill)];
        return catalog;
    }

    private static List<SkillDefinition> AttachCompanions(
        Dictionary<string, SkillDefinition> map,
        SkillDefinition plugin)
    {
        var attached = new List<SkillDefinition>();
        foreach (var companion in plugin.CompanionSkills)
        {
            if (string.IsNullOrWhiteSpace(companion.Id) && string.IsNullOrWhiteSpace(companion.Name))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(companion.Repo))
            {
                companion.Repo = plugin.Repo;
            }

            if (string.IsNullOrWhiteSpace(companion.Source))
            {
                companion.Source = plugin.Source;
            }

            if (string.IsNullOrWhiteSpace(companion.Host))
            {
                companion.Host = plugin.Host;
                companion.GitPath = plugin.GitPath;
            }

            if (companion.Engines.Count == 0)
            {
                companion.Engines = [.. plugin.Engines];
            }

            companion.ParentPluginId = plugin.Id;
            companion.ParentPluginName = plugin.DisplayName;
            Ingest(map, companion, ToolKind.Companion);
            attached.Add(map[companion.Id]);
        }

        return attached;
    }

    private static void Ingest(
        Dictionary<string, SkillDefinition> map,
        SkillDefinition item,
        ToolKind? forcedKind)
    {
        if (string.IsNullOrWhiteSpace(item.Id) && string.IsNullOrWhiteSpace(item.Name))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(item.Id))
        {
            item.Id = item.Name;
        }

        if (string.IsNullOrWhiteSpace(item.Name))
        {
            item.Name = item.Id;
        }

        if (string.IsNullOrWhiteSpace(item.InstallName))
        {
            item.InstallName = item.Id;
        }

        if (string.IsNullOrWhiteSpace(item.SourcePath))
        {
            item.SourcePath = ".";
        }

        if (forcedKind is { } kind)
        {
            item.Kind = kind;
        }
        else if (!Enum.IsDefined(item.Kind))
        {
            item.Kind = ToolKind.Skill;
        }

        item.Engines = [.. GameEngines.Normalize(item.Engines)];
        map[item.Id] = item;
    }

    private void Merge(SkillCatalogFile target, string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var extra = ReadCatalog(path);
        MergeList(target.Skills, extra.Skills);
        MergeList(target.Plugins, extra.Plugins);
        MergeList(target.Packages, extra.Packages);
    }

    private static void MergeList(List<SkillDefinition> target, List<SkillDefinition> extra)
    {
        var map = target.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var item in extra)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                continue;
            }

            map[item.Id] = item;
        }

        target.Clear();
        target.AddRange(map.Values);
    }

    private static SkillCatalogFile ReadCatalog(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SkillCatalogFile>(json, JsonUtil.Options)
               ?? new SkillCatalogFile();
    }

    private static void AddIfExists(List<string> files, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path)
            && File.Exists(path)
            && !files.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            files.Add(path);
        }
    }

    private static string? FindRepoCatalogNear(string start)
    {
        var dir = new DirectoryInfo(start);
        for (var i = 0; i < 6 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir.FullName, "catalog", "skills.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
