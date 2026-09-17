using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public sealed class WorkspaceScanner
{
    public WorkspaceInfo Scan(string workspacePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            throw new ArgumentException("工作区路径不能为空。", nameof(workspacePath));
        }

        var full = Path.GetFullPath(workspacePath);
        var roots = SkillRoots.DetectableRoots
            .Select(name => DescribeRoot(full, name))
            .ToList();

        return new WorkspaceInfo
        {
            Path = full,
            Roots = roots
        };
    }

    public static string SkillsFolder(string workspacePath, string rootName)
    {
        return Path.Combine(Path.GetFullPath(workspacePath), rootName, "skills");
    }

    public static string SkillInstallPath(string workspacePath, string rootName, string installName)
    {
        return Path.Combine(SkillsFolder(workspacePath, rootName), installName);
    }

    public IReadOnlyList<RootInstallStatus> InspectInstalls(
        string workspacePath,
        SkillDefinition skill,
        IEnumerable<string> roots)
    {
        if (skill.IsPlugin)
        {
            var relative = skill.ResolvedInstallPath;
            var path = SkillInstaller.ResolvePluginDestination(workspacePath, skill);
            return [DescribeInstall(path, relative, SkillInstaller.PluginMarkerFileName)];
        }

        var list = new List<RootInstallStatus>();
        foreach (var root in SkillRoots.Normalize(roots))
        {
            list.Add(DescribeSkillInstall(workspacePath, root, skill.ResolvedInstallName));
        }

        return list;
    }

    private static RootInstallStatus DescribeSkillInstall(
        string workspacePath,
        string root,
        string installName)
    {
        foreach (var disk in SkillRoots.DiskNames(root))
        {
            var path = SkillInstallPath(workspacePath, disk, installName);
            if (Directory.Exists(path))
            {
                return DescribeInstall(path, root, SkillInstaller.MarkerFileName);
            }
        }

        return DescribeInstall(
            SkillInstallPath(workspacePath, root, installName),
            root,
            SkillInstaller.MarkerFileName);
    }

    private static RootInstallStatus DescribeInstall(string path, string root, string markerFileName)
    {
        var markerPath = Path.Combine(path, markerFileName);
        var installed = Directory.Exists(path);
        var managed = installed && File.Exists(markerPath);
        var commit = "";
        var branch = "";
        if (managed)
        {
            try
            {
                var marker = SkillInstaller.ReadMarker(markerPath);
                commit = marker?.Commit ?? "";
                branch = marker?.Branch ?? "";
            }
            catch
            {
                managed = false;
            }
        }

        return new RootInstallStatus
        {
            Root = root,
            Installed = installed,
            Managed = managed,
            Commit = commit,
            Branch = branch,
            Path = path
        };
    }

    private static SkillRootInfo DescribeRoot(string workspace, string name)
    {
        var full = Path.Combine(workspace, name);
        var disks = SkillRoots.DiskNames(name);
        var exists = disks.Any(disk => Directory.Exists(Path.Combine(workspace, disk)));
        var hasSkills = disks.Any(disk => Directory.Exists(Path.Combine(workspace, disk, "skills")));
        var canonicalExists = Directory.Exists(full);
        var legacyExists = name.Equals(SkillRoots.DefaultRoot, StringComparison.OrdinalIgnoreCase)
                           && Directory.Exists(Path.Combine(workspace, SkillRoots.LegacyAgentRoot));
        return new SkillRootInfo
        {
            Name = name,
            FullPath = full,
            Exists = exists,
            HasSkillsFolder = hasSkills,
            HasLegacyOnly = legacyExists && !canonicalExists
        };
    }
}
