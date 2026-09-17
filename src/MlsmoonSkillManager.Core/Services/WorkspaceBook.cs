using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.Core.Services;

public static class WorkspaceBook
{
    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        var trimmed = path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        try
        {
            return Path.GetFullPath(trimmed);
        }
        catch (Exception)
        {
            return trimmed;
        }
    }

    public static bool PathsEqual(string? left, string? right)
    {
        return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
    }

    public static string DisplayName(string? path)
    {
        var full = Normalize(path);
        if (string.IsNullOrWhiteSpace(full))
        {
            return "";
        }

        var name = Path.GetFileName(full);
        return string.IsNullOrWhiteSpace(name) ? full : name;
    }

    public static void Migrate(UserSettings settings)
    {
        if (settings.Workspaces.Count == 0 && !string.IsNullOrWhiteSpace(settings.LastWorkspace))
        {
            settings.Workspaces.Add(new WorkspaceEntry
            {
                Path = Normalize(settings.LastWorkspace),
                SelectedRoots = settings.SelectedRoots.Count > 0
                    ? [.. settings.SelectedRoots]
                    : [SkillRoots.DefaultRoot]
            });
        }

        Dedup(settings);
        if (string.IsNullOrWhiteSpace(settings.LastWorkspace) && settings.Workspaces.Count > 0)
        {
            settings.LastWorkspace = settings.Workspaces[0].Path;
        }
    }

    public static void Dedup(UserSettings settings)
    {
        var unique = new List<WorkspaceEntry>();
        foreach (var entry in settings.Workspaces)
        {
            entry.Path = Normalize(entry.Path);
            if (string.IsNullOrWhiteSpace(entry.Path))
            {
                continue;
            }

            if (entry.SelectedRoots.Count == 0)
            {
                entry.SelectedRoots.Add(SkillRoots.DefaultRoot);
            }

            var existing = unique.Find(item => PathsEqual(item.Path, entry.Path));
            if (existing is null)
            {
                unique.Add(entry);
            }
        }

        settings.Workspaces = unique;
    }

    public static WorkspaceEntry? Find(UserSettings settings, string? path)
    {
        var full = Normalize(path);
        return settings.Workspaces.Find(item => PathsEqual(item.Path, full));
    }

    public static WorkspaceEntry AddOrGet(UserSettings settings, string path)
    {
        var full = Normalize(path);
        if (string.IsNullOrWhiteSpace(full))
        {
            throw new ArgumentException("工作区路径不能为空。", nameof(path));
        }

        var existing = Find(settings, full);
        if (existing is not null)
        {
            Touch(settings, existing);
            return existing;
        }

        var entry = new WorkspaceEntry
        {
            Path = full,
            SelectedRoots = [SkillRoots.DefaultRoot],
            LastUsedUtc = DateTime.UtcNow.ToString("o")
        };
        settings.Workspaces.Insert(0, entry);
        settings.LastWorkspace = full;
        return entry;
    }

    public static void Touch(UserSettings settings, WorkspaceEntry entry)
    {
        entry.LastUsedUtc = DateTime.UtcNow.ToString("o");
        settings.LastWorkspace = entry.Path;
        settings.Workspaces.RemoveAll(item => PathsEqual(item.Path, entry.Path));
        settings.Workspaces.Insert(0, entry);
    }

    public static bool Remove(UserSettings settings, string path)
    {
        var removed = settings.Workspaces.RemoveAll(item => PathsEqual(item.Path, path)) > 0;
        if (removed && PathsEqual(settings.LastWorkspace, path))
        {
            settings.LastWorkspace = settings.Workspaces.FirstOrDefault()?.Path ?? "";
        }

        return removed;
    }
}
