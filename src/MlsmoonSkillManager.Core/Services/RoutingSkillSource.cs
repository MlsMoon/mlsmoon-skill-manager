using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class RoutingSkillAlign
{
    public bool SourceExists { get; init; }
    public bool DestExists { get; init; }
    public string SourcePath { get; init; } = "";
    public string DestPath { get; init; } = "";
    public IReadOnlyList<GitChange> Changes { get; init; } = [];

    public string Summary
    {
        get
        {
            if (!SourceExists)
            {
                return DestExists ? "源目录还不存在，可同步到源写入。" : "源目录还不存在。";
            }

            if (!DestExists)
            {
                return "工作区还没有副本。";
            }

            return Changes.Count == 0
                ? "与源一致"
                : $"工作区相对源有 {Changes.Count} 个文件不同";
        }
    }
}

public static class RoutingSkillSource
{
    public const string ProjectSkillFolder = "project-skill";

    public static string RelativeSourcePath(string skillId) =>
        string.Join('/', MlsmoonSkillConfig.FolderName, ProjectSkillFolder, skillId.Trim());

    public static string PluginSourceDirectory(string pluginDest, string skillId) =>
        Path.Combine(pluginDest, MlsmoonSkillConfig.FolderName, ProjectSkillFolder, skillId.Trim());

    public static RoutingSkillAlign Inspect(
        SkillDefinition plugin,
        SkillDefinition routing,
        string workspacePath,
        IReadOnlyList<string> roots)
    {
        var paths = Resolve(plugin, routing, workspacePath, roots);
        return Compare(paths.Source, paths.Dest);
    }

    public static RoutingSkillAlign CopyFromSource(
        SkillDefinition plugin,
        SkillDefinition routing,
        string workspacePath,
        IReadOnlyList<string> roots,
        Action<string>? log)
    {
        var paths = Resolve(plugin, routing, workspacePath, roots);
        if (!Directory.Exists(paths.Source))
        {
            throw new InvalidOperationException("源目录还不存在。");
        }

        Directory.CreateDirectory(paths.Dest);
        CopyOverlay(paths.Source, paths.Dest);
        log?.Invoke($"从源同步 {routing.DisplayName} ← {RelativeSourcePath(routing.Id)}");
        return Compare(paths.Source, paths.Dest);
    }

    public static RoutingSkillAlign CopyToSource(
        SkillDefinition plugin,
        SkillDefinition routing,
        string workspacePath,
        IReadOnlyList<string> roots,
        Action<string>? log)
    {
        var paths = Resolve(plugin, routing, workspacePath, roots);
        if (!Directory.Exists(paths.Dest))
        {
            throw new InvalidOperationException("工作区还没有路由 Skill 副本。");
        }

        Directory.CreateDirectory(paths.Source);
        CopyOverlay(paths.Dest, paths.Source);
        log?.Invoke($"同步到源 {routing.DisplayName} → {RelativeSourcePath(routing.Id)}");
        return Compare(paths.Source, paths.Dest);
    }

    public static RoutingSkillAlign Compare(string source, string dest)
    {
        var sourceExists = HasSkillFiles(source);
        var destExists = Directory.Exists(dest);
        var changes = sourceExists
            ? InstallSnapshot.Diff(HashSkillTree(source), HashSkillTree(dest))
            : [];
        return new RoutingSkillAlign
        {
            SourceExists = sourceExists,
            DestExists = destExists,
            SourcePath = source,
            DestPath = dest,
            Changes = changes
        };
    }

    public static void CopyOverlay(string from, string to)
    {
        var toRoot = Path.GetFullPath(to).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(to);
        foreach (var file in SkillFiles(from))
        {
            var relative = Path.GetRelativePath(from, file);
            var target = Path.GetFullPath(Path.Combine(to, relative));
            if (!target.StartsWith(toRoot, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static (string Source, string Dest) Resolve(
        SkillDefinition plugin,
        SkillDefinition routing,
        string workspacePath,
        IReadOnlyList<string> roots)
    {
        var pluginDest = ProjectCopy.ResolveDestination(workspacePath, plugin);
        var source = PluginSourceDirectory(pluginDest, routing.Id);
        var dest = MlsmoonSkillConfig.FindDirectory(workspacePath, roots, routing.Id)
                   ?? MlsmoonSkillConfig.ResolveInstallDirectory(
                       workspacePath,
                       SkillRoots.Normalize(roots).FirstOrDefault() ?? SkillRoots.DefaultRoot,
                       routing.Id);
        return (source, dest);
    }

    private static Dictionary<string, string> HashSkillTree(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return InstallSnapshot.HashTree(directory)
            .Where(entry => !IsToolOwned(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> SkillFiles(string root)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (var file in Directory.GetFiles(root))
        {
            if (!InstallSnapshot.IgnoreNames.Contains(Path.GetFileName(file)))
            {
                yield return file;
            }
        }

        foreach (var dir in Directory.GetDirectories(root))
        {
            var name = Path.GetFileName(dir);
            if (InstallSnapshot.IgnoreNames.Contains(name) || IsToolFolder(name))
            {
                continue;
            }

            foreach (var nested in SkillFiles(dir))
            {
                yield return nested;
            }
        }
    }

    private static bool HasSkillFiles(string directory) =>
        Directory.Exists(directory) && SkillFiles(directory).Any();

    private static bool IsToolOwned(string relative)
    {
        var first = relative.Split('/', '\\')[0];
        return IsToolFolder(first);
    }

    private static bool IsToolFolder(string name) =>
        name.Equals(MlsmoonSkillConfig.FolderName, StringComparison.OrdinalIgnoreCase);
}
