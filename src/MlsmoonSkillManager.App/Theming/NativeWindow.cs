using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Shell;
using MlsmoonSkillManager.Core.Services;

namespace MlsmoonSkillManager.App.Theming;

public static class NativeWindow
{
    public const string UiTestId = "MlsmoonUiTest";

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmWcpRound = 2;
    private const int DwmSbtNone = 1;
    private const int GwlExstyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolwindow = 0x00000080;
    private const int WsExAppwindow = 0x00040000;

    public static void ParkForUiTest(Window window)
    {
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.Topmost = false;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -24000;
        window.Top = 0;
        AutomationProperties.SetAutomationId(window, UiTestId);
        AutomationProperties.SetName(window, "Moon Game Dev Tool Manager — UI TEST");
    }

    public static void QuietChrome(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var style = GetExStyle(hwnd).ToInt64() | WsExNoActivate | WsExToolwindow;
        style &= ~WsExAppwindow;
        SetExStyle(hwnd, new IntPtr(style));
    }

    public static void ApplyChrome(Window window)
    {
        window.WindowStyle = WindowStyle.None;
        window.ResizeMode = ResizeMode.CanResize;
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = 48,
            ResizeBorderThickness = SystemParameters.WindowResizeBorderThickness,
            GlassFrameThickness = new Thickness(0),
            CornerRadius = default,
            UseAeroCaptionButtons = false
        });
        ApplyCaptionTheme(window);
        if (Application.Current is App { IsUiTest: true })
        {
            QuietChrome(window);
        }
    }

    public static void ApplyCaptionTheme(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            hwnd = new WindowInteropHelper(window).EnsureHandle();
        }

        var dark = ThemeManager.Effective == ThemeResolver.Dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
        var round = DwmWcpRound;
        DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref round, sizeof(int));
        var backdrop = DwmSbtNone;
        DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
    }

    private static IntPtr GetExStyle(IntPtr hwnd) =>
        IntPtr.Size == 8 ? GetWindowLongPtr(hwnd, GwlExstyle) : new IntPtr(GetWindowLong(hwnd, GwlExstyle));

    private static void SetExStyle(IntPtr hwnd, IntPtr value)
    {
        if (IntPtr.Size == 8)
        {
            SetWindowLongPtr(hwnd, GwlExstyle, value);
            return;
        }

        SetWindowLong(hwnd, GwlExstyle, value.ToInt32());
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
}
