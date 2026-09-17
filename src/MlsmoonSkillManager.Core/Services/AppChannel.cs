namespace MlsmoonSkillManager.Core.Services;

public static class AppChannel
{
    public const string Dev = "dev";
    public const string Exe = "exe";
    public const string DevFlag = "--dev";

    public static bool IsDev(IEnumerable<string>? args, bool debugBuild)
    {
        if (debugBuild)
        {
            return true;
        }

        return args is not null
               && args.Any(item => string.Equals(item, DevFlag, StringComparison.OrdinalIgnoreCase));
    }

    public static string Name(bool isDev) => isDev ? Dev : Exe;

    public static string MutexName(bool isDev) =>
        $@"Local\MoonGameDevToolManager.{Name(isDev)}";

    public static string ActivateEventName(bool isDev) =>
        $@"Local\MoonGameDevToolManager.{Name(isDev)}.activate";
}
