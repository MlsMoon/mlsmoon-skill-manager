namespace MlsmoonSkillManager.Core.Models;

public static class SkillRoots
{
    public const string DefaultRoot = ".agent";

    public static readonly IReadOnlyList<string> PrimaryRoots = [".agent", ".claude", ".grok"];

    public static readonly IReadOnlyList<string> DetectableRoots =
        [".agent", ".agents", ".claude", ".grok"];
}

public sealed class SkillRootInfo
{
    public required string Name { get; init; }
    public required string FullPath { get; init; }
    public bool Exists { get; init; }
    public bool HasSkillsFolder { get; init; }
    public bool IsDefault => Name.Equals(SkillRoots.DefaultRoot, StringComparison.OrdinalIgnoreCase);
}

public sealed class WorkspaceInfo
{
    public required string Path { get; init; }
    public IReadOnlyList<SkillRootInfo> Roots { get; init; } = [];
}

public sealed class InstallMarker
{
    public string Id { get; set; } = "";
    public string Repo { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string InstalledAtUtc { get; set; } = "";
    public string Commit { get; set; } = "";
    public string Branch { get; set; } = "";
    public string ManagerVersion { get; set; } = "";
    public string ParentPluginId { get; set; } = "";
}

public sealed class RootInstallStatus
{
    public required string Root { get; init; }
    public bool Installed { get; init; }
    public bool Managed { get; init; }
    public string Commit { get; init; } = "";
    public string Branch { get; init; } = "";
    public string Path { get; init; } = "";
}

public sealed class WorkspaceEntry
{
    public string Path { get; set; } = "";
    public List<string> SelectedRoots { get; set; } = [SkillRoots.DefaultRoot];
    public string LastUsedUtc { get; set; } = "";
}

public sealed class UserSettings
{
    public string LastWorkspace { get; set; } = "";
    public List<string> SelectedRoots { get; set; } = [SkillRoots.DefaultRoot];
    public List<WorkspaceEntry> Workspaces { get; set; } = [];
    public string Theme { get; set; } = "System";
    public bool ShowRepoLinks { get; set; } = true;
    public string NasUser { get; set; } = "";
    public bool AutoCheckUpdates { get; set; } = true;
}
