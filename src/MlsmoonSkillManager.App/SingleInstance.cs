using System.Threading;
using System.Windows;
using MlsmoonSkillManager.Core.Services;

namespace MlsmoonSkillManager.App;

public sealed class SingleInstance : IDisposable
{
    private Mutex? _mutex;
    private EventWaitHandle? _activate;
    private Thread? _watcher;
    private bool _owned;
    private volatile bool _stop;

    public SingleInstance(bool isDev)
    {
        IsDev = isDev;
        Channel = AppChannel.Name(isDev);
    }

    public bool IsDev { get; }
    public string Channel { get; }

    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, AppChannel.MutexName(IsDev), out var createdNew);
        _owned = createdNew;
        _activate = new EventWaitHandle(false, EventResetMode.AutoReset, AppChannel.ActivateEventName(IsDev));
        if (!createdNew)
        {
            _activate.Set();
            return false;
        }

        _watcher = new Thread(WatchActivate)
        {
            IsBackground = true,
            Name = "MoonGameDevToolManager.Activate"
        };
        _watcher.Start();
        return true;
    }

    public void Dispose()
    {
        _stop = true;
        _activate?.Set();
        if (_owned)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }
        }

        _mutex?.Dispose();
        _activate?.Dispose();
        _mutex = null;
        _activate = null;
    }

    private void WatchActivate()
    {
        var signal = _activate;
        if (signal is null)
        {
            return;
        }

        while (true)
        {
            try
            {
                signal.WaitOne();
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            var app = Application.Current;
            if (_stop || app is null)
            {
                return;
            }

            app.Dispatcher.BeginInvoke(ActivateMainWindow);
        }
    }

    private static void ActivateMainWindow()
    {
        if (Application.Current?.MainWindow is not { } window)
        {
            return;
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Show();
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }
}
