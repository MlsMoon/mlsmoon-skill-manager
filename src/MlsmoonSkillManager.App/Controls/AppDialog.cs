using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MlsmoonSkillManager.App.Controls;

public class AppDialog : HeaderedContentControl
{
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen),
        typeof(bool),
        typeof(AppDialog),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));

    public static readonly DependencyProperty PrimaryTextProperty = DependencyProperty.Register(
        nameof(PrimaryText),
        typeof(string),
        typeof(AppDialog),
        new PropertyMetadata("确定"));

    public static readonly DependencyProperty SecondaryTextProperty = DependencyProperty.Register(
        nameof(SecondaryText),
        typeof(string),
        typeof(AppDialog),
        new PropertyMetadata(""));

    public static readonly DependencyProperty PrimaryCommandProperty = DependencyProperty.Register(
        nameof(PrimaryCommand),
        typeof(ICommand),
        typeof(AppDialog));

    public static readonly DependencyProperty SecondaryCommandProperty = DependencyProperty.Register(
        nameof(SecondaryCommand),
        typeof(ICommand),
        typeof(AppDialog));

    public static readonly DependencyProperty CloseOnOverlayProperty = DependencyProperty.Register(
        nameof(CloseOnOverlay),
        typeof(bool),
        typeof(AppDialog),
        new PropertyMetadata(true));

    public static readonly DependencyProperty CloseOnPrimaryProperty = DependencyProperty.Register(
        nameof(CloseOnPrimary),
        typeof(bool),
        typeof(AppDialog),
        new PropertyMetadata(true));

    public static readonly DependencyProperty AllowDismissProperty = DependencyProperty.Register(
        nameof(AllowDismiss),
        typeof(bool),
        typeof(AppDialog),
        new PropertyMetadata(true));

    public static readonly DependencyProperty CardMaxWidthProperty = DependencyProperty.Register(
        nameof(CardMaxWidth),
        typeof(double),
        typeof(AppDialog),
        new PropertyMetadata(640.0));

    public static readonly DependencyProperty CardMinWidthProperty = DependencyProperty.Register(
        nameof(CardMinWidth),
        typeof(double),
        typeof(AppDialog),
        new PropertyMetadata(440.0));

    static AppDialog() => ThemeChrome.Default<AppDialog>();

    public AppDialog() => ThemeChrome.UseAppStyle(this);

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public string PrimaryText
    {
        get => (string)GetValue(PrimaryTextProperty);
        set => SetValue(PrimaryTextProperty, value);
    }

    public string SecondaryText
    {
        get => (string)GetValue(SecondaryTextProperty);
        set => SetValue(SecondaryTextProperty, value);
    }

    public ICommand? PrimaryCommand
    {
        get => (ICommand?)GetValue(PrimaryCommandProperty);
        set => SetValue(PrimaryCommandProperty, value);
    }

    public ICommand? SecondaryCommand
    {
        get => (ICommand?)GetValue(SecondaryCommandProperty);
        set => SetValue(SecondaryCommandProperty, value);
    }

    public bool CloseOnOverlay
    {
        get => (bool)GetValue(CloseOnOverlayProperty);
        set => SetValue(CloseOnOverlayProperty, value);
    }

    public bool CloseOnPrimary
    {
        get => (bool)GetValue(CloseOnPrimaryProperty);
        set => SetValue(CloseOnPrimaryProperty, value);
    }

    public bool AllowDismiss
    {
        get => (bool)GetValue(AllowDismissProperty);
        set => SetValue(AllowDismissProperty, value);
    }

    public double CardMaxWidth
    {
        get => (double)GetValue(CardMaxWidthProperty);
        set => SetValue(CardMaxWidthProperty, value);
    }

    public double CardMinWidth
    {
        get => (double)GetValue(CardMinWidthProperty);
        set => SetValue(CardMinWidthProperty, value);
    }

    public void Close() => IsOpen = false;

    private int _hideGeneration;

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((AppDialog)d).ApplyOpenState(animate: true);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_Overlay") is UIElement overlay)
        {
            overlay.MouseLeftButtonDown += (_, args) =>
            {
                if (CloseOnOverlay && AllowDismiss && args.OriginalSource == overlay)
                {
                    Close();
                }
            };
        }

        if (GetTemplateChild("PART_Close") is Button close)
        {
            close.Click += (_, _) =>
            {
                if (AllowDismiss)
                {
                    Close();
                }
            };
        }

        if (GetTemplateChild("PART_Primary") is Button primary)
        {
            primary.Click += (_, _) =>
            {
                if (CloseOnPrimary)
                {
                    Close();
                }
            };
        }

        ApplyOpenState(animate: false);
    }

    private void ApplyOpenState(bool animate)
    {
        if (!IsLoaded && Template is null)
        {
            return;
        }

        if (IsOpen)
        {
            _hideGeneration++;
            Visibility = Visibility.Visible;
            VisualStateManager.GoToState(this, "Open", animate);
            return;
        }

        VisualStateManager.GoToState(this, "Closed", animate);
        if (!animate)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        var generation = ++_hideGeneration;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(140) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (generation == _hideGeneration && !IsOpen)
            {
                Visibility = Visibility.Collapsed;
            }
        };
        timer.Start();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (IsOpen && e.Key == Key.Escape && AllowDismiss)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
