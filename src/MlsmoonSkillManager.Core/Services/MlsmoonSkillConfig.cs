using System.Text.Json;
using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public static class MlsmoonSkillConfig
{
    public const string FolderName = ".mlsmoon";
    public const string FileName = "skill.json";

    public static string FilePath(string skillDirectory) =>
        Path.Combine(skillDirectory, FolderName, FileName);

    public static MlsmoonSkillFile? Read(string? skillDirectory)
    {
        if (string.IsNullOrWhiteSpace(skillDirectory) || !Directory.Exists(skillDirectory))
        {
            return null;
        }

        var path = FilePath(skillDirectory);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<MlsmoonSkillFile>(File.ReadAllText(path), JsonUtil.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static void Write(string skillDirectory, MlsmoonSkillFile file)
    {
        Directory.CreateDirectory(Path.Combine(skillDirectory, FolderName));
        File.WriteAllText(FilePath(skillDirectory), JsonSerializer.Serialize(file, JsonUtil.Options));
    }

    public static void WriteIdentity(string skillDirectory, SkillDefinition skill)
    {
        Write(skillDirectory, IdentityFrom(skill));
    }

    public static MlsmoonSkillFile IdentityFrom(SkillDefinition skill) => new()
    {
        Id = skill.Id,
        Kind = skill.IsRouting ? "routing" : "skill",
        Name = skill.DisplayName,
        Repo = string.IsNullOrWhiteSpace(skill.Repo) ? null : skill.Repo,
        ParentId = string.IsNullOrWhiteSpace(skill.ParentPluginId) ? null : skill.ParentPluginId
    };

    public static SkillDefinition ToRoutingCompanion(SkillDefinition plugin, MlsmoonSkillFile config)
    {
        var id = string.IsNullOrWhiteSpace(config.Id) ? plugin.Id + "-skill" : config.Id.Trim();
        var name = string.IsNullOrWhiteSpace(config.Name) ? id : config.Name.Trim();
        var description = string.IsNullOrWhiteSpace(config.Description)
            ? $"{plugin.DisplayName} 的路由 skill。改该插件前先读本文件，再只打开对应的包内 skill。"
            : config.Description.Trim();
        return new SkillDefinition
        {
            Id = id,
            Name = name,
            Description = description,
            Kind = ToolKind.Companion,
            IsRouting = true,
            Repo = plugin.Repo,
            Source = plugin.Source,
            Host = plugin.Host,
            GitPath = plugin.GitPath,
            Engines = [.. plugin.Engines],
            ParentPluginId = plugin.Id,
            ParentPluginName = plugin.DisplayName,
            InstallName = id,
            SourcePath = RoutingSkillSource.RelativeSourcePath(id)
        };
    }

    public static string? FindDirectory(string workspacePath, IEnumerable<string> roots, string id)
    {
        if (string.IsNullOrWhiteSpace(workspacePath) || string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        foreach (var disk in SkillRoots.Expand(roots))
        {
            var found = ExistingInRoot(workspacePath, disk, id);
            if (!string.IsNullOrWhiteSpace(found))
            {
                return found;
            }
        }

        return null;
    }

    public static string ResolveInstallDirectory(string workspacePath, string rootName, string id)
    {
        foreach (var disk in SkillRoots.DiskNames(rootName))
        {
            var found = ExistingInRoot(workspacePath, disk, id);
            if (!string.IsNullOrWhiteSpace(found))
            {
                return found;
            }
        }

        return WorkspaceScanner.SkillInstallPath(workspacePath, rootName, id);
    }

    private static string? ExistingInRoot(string workspacePath, string rootName, string id)
    {
        var folder = WorkspaceScanner.SkillsFolder(workspacePath, rootName);
        if (Directory.Exists(folder))
        {
            foreach (var dir in Directory.GetDirectories(folder))
            {
                var file = Read(dir);
                if (file is not null && file.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                {
                    return dir;
                }
            }
        }

        var named = WorkspaceScanner.SkillInstallPath(workspacePath, rootName, id);
        return Directory.Exists(named) ? named : null;
    }
}
