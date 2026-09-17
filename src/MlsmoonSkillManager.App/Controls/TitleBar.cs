using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MlsmoonSkillManager.App.Controls;

public class TitleBar : Control
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(TitleBar),
        new PropertyMetadata(""));

    public static readonly DependencyProperty ExtraProperty = DependencyProperty.Register(
        nameof(Extra),
        typeof(object),
        typeof(TitleBar));

    public static readonly DependencyProperty IsMaximizedProperty = DependencyProperty.Register(
        nameof(IsMaximized),
        typeof(bool),
        typeof(TitleBar),
        new PropertyMetadata(false));

    static TitleBar() => ThemeChrome.Default<TitleBar>();

    public TitleBar()
    {
        ThemeChrome.UseAppStyle(this);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? Extra
    {
        get => GetValue(ExtraProperty);
        set => SetValue(ExtraProperty, value);
    }

    public bool IsMaximized
    {
        get => (bool)GetValue(IsMaximizedProperty);
        set => SetValue(IsMaximizedProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        BindClick("PART_Minimize", Minimize);
        BindClick("PART_Maximize", ToggleMaximize);
        BindClick("PART_Close", Close);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } window)
        {
            window.StateChanged += OnWindowStateChanged;
            SyncMaximized(window);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } window)
        {
            window.StateChanged -= OnWindowStateChanged;
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            SyncMaximized(window);
        }
    }

    private void SyncMaximized(Window window) => IsMaximized = window.WindowState == WindowState.Maximized;

    private void Minimize()
    {
        if (Window.GetWindow(this) is { } window)
        {
            window.WindowState = WindowState.Minimized;
        }
    }

    private void ToggleMaximize()
    {
        if (Window.GetWindow(this) is { } window)
        {
            window.WindowState = window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }
    }

    private void Close() => Window.GetWindow(this)?.Close();

    private void BindClick(string name, Action action)
    {
        if (GetTemplateChild(name) is Button button)
        {
            button.Click += (_, _) => action();
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (FindsButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        Window.GetWindow(this)?.DragMove();
    }

    private static bool FindsButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Button)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
