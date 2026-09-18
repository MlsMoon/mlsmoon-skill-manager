using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public static class ProjectCopy
{
    public const string PluginMarker = ".mlsmoon-plugin.json";
    public const string PackageMarker = ".mlsmoon-package.json";

    public static readonly HashSet<string> SkipNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".github", ".vs", "bin", "obj", ".idea"
    };

    public static string MarkerFileName(SkillDefinition skill) =>
        skill.IsPackage ? PackageMarker : PluginMarker;

    public static string? FindMarker(string dest)
    {
        foreach (var name in new[] { PackageMarker, PluginMarker })
        {
            var path = Path.Combine(dest, name);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    public static string ResolveDestination(string workspacePath, SkillDefinition item)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            throw new ArgumentException("工作区路径不能为空。", nameof(workspacePath));
        }

        var workspace = Path.GetFullPath(workspacePath);
        var relative = item.ResolvedInstallPath;
        if (string.IsNullOrWhiteSpace(relative)
            || Path.IsPathRooted(relative)
            || relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part is ".." or "."))
        {
            throw new InvalidOperationException($"{item.KindLabel} {item.DisplayName} 的 installPath 无效: {item.InstallPath}");
        }

        var dest = Path.GetFullPath(Path.Combine(workspace, relative));
        var prefix = workspace.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        if (!dest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{item.KindLabel} {item.DisplayName} 的安装路径必须位于工作区内。");
        }

        return dest;
    }
}
