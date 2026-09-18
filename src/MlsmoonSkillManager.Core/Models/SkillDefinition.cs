namespace MlsmoonSkillManager.Core.Models;

public sealed class SkillCatalogFile
{
    public int Version { get; set; } = 1;
    public string Owner { get; set; } = "MlsMoon";
    public List<SkillDefinition> Skills { get; set; } = [];
    public List<SkillDefinition> Plugins { get; set; } = [];
    public List<SkillDefinition> Packages { get; set; } = [];
}

public sealed class SkillDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public ToolKind Kind { get; set; } = ToolKind.Skill;
    public string Repo { get; set; } = "";
    public string Description { get; set; } = "";
    public string SourcePath { get; set; } = ".";
    public string InstallName { get; set; } = "";
    public string InstallPath { get; set; } = "";
    public string ParentPluginId { get; set; } = "";
    public string ParentPluginName { get; set; } = "";
    public List<SkillDefinition> CompanionSkills { get; set; } = [];
    public List<string> Engines { get; set; } = [];
    public string Source { get; set; } = "github";
    public string Host { get; set; } = "";
    public string GitPath { get; set; } = "";
    public string ReadmePath { get; set; } = "Readme.md";
    public List<LanGitBranch> Branches { get; set; } = [];

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;

    public IReadOnlyList<string> ResolvedEngines => GameEngines.Normalize(Engines);

    public bool IsUniversal => GameEngines.IsUniversal(Engines);

    public bool IsLan =>
        Source.Equals("lan", StringComparison.OrdinalIgnoreCase)
        || !string.IsNullOrWhiteSpace(Host);

    public bool IsPlugin => Kind == ToolKind.Plugin;

    public bool IsPackage => Kind == ToolKind.Package;

    public bool IsProjectCopy => IsPlugin || IsPackage;

    public bool IsCompanion => Kind == ToolKind.Companion;

    public string KindLabel => Kind switch
    {
        ToolKind.Package => "Package",
        ToolKind.Plugin => "Plugin",
        ToolKind.Companion => "随附 Skill",
        _ => "Skill"
    };

    public string ResolvedInstallName =>
        string.IsNullOrWhiteSpace(InstallName) ? Id : InstallName;

    public string ResolvedSourcePath =>
        string.IsNullOrWhiteSpace(SourcePath) ? "." : SourcePath;

    public bool IsRepoRoot =>
        !IsProjectCopy && ResolvedSourcePath is "." or "./";

    public string ResolvedInstallPath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(InstallPath))
            {
                return InstallPath.Replace('/', Path.DirectorySeparatorChar)
                    .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            return IsPackage
                ? Path.Combine("Packages", ResolvedInstallName)
                : IsPlugin
                    ? Path.Combine("Assets", "Plugins", ResolvedInstallName)
                    : ResolvedInstallName;
        }
    }

    public string SshUrl(string user)
    {
        if (string.IsNullOrWhiteSpace(Host) || string.IsNullOrWhiteSpace(GitPath))
        {
            throw new InvalidOperationException($"{DisplayName} 缺少局域网 host / gitPath。");
        }

        var host = Host.Trim();
        var path = GitPath.Trim();
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        var account = string.IsNullOrWhiteSpace(user) ? Environment.UserName : user.Trim();
        return $"ssh://{account}@{host}:{path}";
    }
}

public sealed class LanGitBranch
{
    public string Name { get; set; } = "";
    public string Unity { get; set; } = "";
    public Dictionary<string, string> Manifest { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
