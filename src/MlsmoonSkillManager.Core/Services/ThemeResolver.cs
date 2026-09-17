namespace MlsmoonSkillManager.Core.Services;

public static class ThemeResolver
{
    public const string Dark = "Dark";
    public const string Light = "Light";
    public const string System = "System";

    public static readonly IReadOnlyList<string> Preferences = [Light, Dark, System];

    public static string Normalize(string? value)
    {
        if (string.Equals(value, Light, StringComparison.OrdinalIgnoreCase))
        {
            return Light;
        }

        if (string.Equals(value, Dark, StringComparison.OrdinalIgnoreCase))
        {
            return Dark;
        }

        return System;
    }

    public static string Resolve(string? preference, bool windowsAppsUseLightTheme)
    {
        return Normalize(preference) == System
            ? (windowsAppsUseLightTheme ? Light : Dark)
            : Normalize(preference);
    }

    public static string Label(string preference)
    {
        return Normalize(preference) switch
        {
            Light => "浅色",
            Dark => "深色",
            _ => "系统"
        };
    }
}
