using System.Windows;
using MlsmoonSkillManager.App.Theming;
using MlsmoonSkillManager.Core.Services;

namespace MlsmoonSkillManager.App;

public partial class App : Application
{
    private SingleInstance? _instance;

    public bool IsDev => _instance?.IsDev == true;

    protected override void OnStartup(StartupEventArgs e)
    {
#if DEBUG
        const bool debugBuild = true;
#else
        const bool debugBuild = false;
#endif
        var isDev = AppChannel.IsDev(e.Args, debugBuild);
        _instance = new SingleInstance(isDev);
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
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _instance?.Dispose();
        base.OnExit(e);
    }
}
