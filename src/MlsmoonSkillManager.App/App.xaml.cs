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

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }
}
