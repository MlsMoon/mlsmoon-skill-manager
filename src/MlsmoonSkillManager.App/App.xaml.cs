using System.Diagnostics;
using System.IO;
using System.Windows;
using MlsmoonSkillManager.App.Theming;
using MlsmoonSkillManager.Core.Services;

namespace MlsmoonSkillManager.App;

public partial class App : Application
{
    private SingleInstance? _instance;

    public bool IsDev => _instance?.IsDev == true;

    public bool IsUiTest { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
#if DEBUG
        const bool debugBuild = true;
#else
        const bool debugBuild = false;
#endif
        var isDev = AppChannel.IsDev(e.Args, debugBuild);
        IsUiTest = AppChannel.IsUiTest(e.Args);
        _instance = new SingleInstance(isDev, IsUiTest);
        if (!_instance.TryAcquire())
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Shutdown();
            return;
        }

        var settings = new SettingsStore(new AppPaths()).Load();
        ThemeManager.Apply(settings.Theme);
        ThemeManager.WatchSystem();
        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        if (IsUiTest)
        {
            NativeWindow.ParkForUiTest(window);
        }

        window.Show();
        if (IsUiTest)
        {
            var ready = Path.Combine(new AppPaths().ConfigDirectory, "ui-ready.log");
            Directory.CreateDirectory(Path.GetDirectoryName(ready)!);
            File.WriteAllText(ready, NativeWindow.UiTestId);
        }
    }

    public void RestartDev()
    {
        if (!IsDev || IsUiTest)
        {
            return;
        }

        var bat = FindRundevBat();
        if (bat is null)
        {
            MessageBox.Show(
                "找不到 Scripts\\rundev.bat，无法重新编译启动。",
                "重启 DEV",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(bat)!, ".."));
        var cmd = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
        var start = new ProcessStartInfo
        {
            FileName = cmd,
            Arguments = $"/c \"\"{bat}\" -WaitPid {Environment.ProcessId}\"",
            UseShellExecute = true,
            WorkingDirectory = repoRoot
        };

        try
        {
            Process.Start(start);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "重启 DEV", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Shutdown();
    }

    private static string? FindRundevBat()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var origin in new[]
                 {
                     Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty),
                     AppContext.BaseDirectory,
                     Environment.CurrentDirectory
                 })
        {
            for (var dir = origin;
                 !string.IsNullOrWhiteSpace(dir) && seen.Add(dir);
                 dir = Path.GetDirectoryName(dir))
            {
                var bat = Path.Combine(dir, "Scripts", "rundev.bat");
                if (File.Exists(bat))
                {
                    return Path.GetFullPath(bat);
                }
            }
        }

        return null;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }
}
