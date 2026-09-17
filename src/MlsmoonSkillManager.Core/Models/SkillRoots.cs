namespace MlsmoonSkillManager.Core.Models;

public static class SkillRoots
{
    public const string DefaultRoot = ".agents";
    public const string LegacyAgentRoot = ".agent";
    public const string CodexRoot = ".codex";

    public static readonly IReadOnlyList<string> DetectableRoots =
        [DefaultRoot, ".claude", ".grok", CodexRoot];

    public static readonly IReadOnlyList<string> PrimaryRoots = DetectableRoots;

    public static string Canonical(string? name)
    {
        var trimmed = (name ?? "").Trim().TrimStart('/', '\\');
        if (trimmed.Length == 0)
        {
            return DefaultRoot;
        }

        return trimmed.Equals(LegacyAgentRoot, StringComparison.OrdinalIgnoreCase)
            ? DefaultRoot
            : trimmed;
    }

    public static IReadOnlyList<string> Normalize(IEnumerable<string>? roots)
    {
        var names = new List<string>();
        foreach (var root in roots ?? [])
        {
            var name = Canonical(root);
            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(name);
            }
        }

        return names.Count > 0 ? names : [DefaultRoot];
    }

    public static IReadOnlyList<string> DiskNames(string root)
    {
        var canonical = Canonical(root);
        return canonical.Equals(DefaultRoot, StringComparison.OrdinalIgnoreCase)
            ? [DefaultRoot, LegacyAgentRoot]
            : [canonical];
    }

    public static IReadOnlyList<string> Expand(IEnumerable<string>? roots)
    {
        var names = new List<string>();
        foreach (var disk in Normalize(roots).SelectMany(DiskNames))
        {
            if (!names.Contains(disk, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(disk);
            }
        }

        return names;
    }
}
