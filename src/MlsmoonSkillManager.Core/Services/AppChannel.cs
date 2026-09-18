namespace MlsmoonSkillManager.Core.Services;

public static class AppChannel
{
    public const string Dev = "dev";
    public const string Exe = "exe";
    public const string UiTest = "uitest";
    public const string DevFlag = "--dev";
    public const string UiTestFlag = "--ui-test";

    public static bool IsDev(IEnumerable<string>? args, bool debugBuild)
    {
        if (debugBuild)
        {
            return true;
        }

        return args is not null
               && args.Any(item => string.Equals(item, DevFlag, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsUiTest(IEnumerable<string>? args)
    {
        foreach (var item in args ?? [])
        {
            if (string.Equals(item, UiTestFlag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return Environment.GetCommandLineArgs().Any(item =>
            string.Equals(item, UiTestFlag, StringComparison.OrdinalIgnoreCase));
    }

    public static string Name(bool isDev, bool uiTest = false) =>
        uiTest ? UiTest : isDev ? Dev : Exe;

    public static string MutexName(bool isDev, bool uiTest = false) =>
        $@"Local\MoonGameDevToolManager.{Name(isDev, uiTest)}";

    public static string ActivateEventName(bool isDev, bool uiTest = false) =>
        $@"Local\MoonGameDevToolManager.{Name(isDev, uiTest)}.activate";
}
