namespace MlsmoonSkillManager.Core.Models;

public static class GameEngines
{
    public const string All = "all";
    public const string Unity = "unity";
    public const string Godot = "godot";

    public static readonly IReadOnlyList<string> Supported = [Unity, Godot];

    public static IReadOnlyList<string> Normalize(IEnumerable<string>? values)
    {
        var tokens = (values ?? [])
            .Select(value => value.Trim().ToLowerInvariant())
            .Where(value => value.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (tokens.Count == 0 || tokens.Contains(All) || tokens.Contains("*"))
        {
            return Supported;
        }

        var resolved = Supported.Where(tokens.Contains).ToList();
        return resolved.Count == 0 ? Supported : resolved;
    }

    public static bool IsUniversal(IEnumerable<string>? values)
    {
        var resolved = Normalize(values);
        return resolved.Count == Supported.Count && Supported.All(resolved.Contains);
    }

    public static string Label(string engine) => engine.Trim().ToLowerInvariant() switch
    {
        Unity => "Unity",
        Godot => "Godot",
        All => "全引擎",
        _ => engine
    };
}
