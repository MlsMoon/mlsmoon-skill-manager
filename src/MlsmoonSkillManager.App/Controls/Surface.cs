using System.Windows;
using System.Windows.Controls;

namespace MlsmoonSkillManager.App.Controls;

public class Surface : HeaderedContentControl
{
    static Surface() => ThemeChrome.Default<Surface>();

    public Surface() => ThemeChrome.UseAppStyle(this);

    public static readonly DependencyProperty HeaderActionProperty = DependencyProperty.Register(
        nameof(HeaderAction),
        typeof(object),
        typeof(Surface));

    public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
        nameof(Footer),
        typeof(object),
        typeof(Surface));

    public object? HeaderAction
    {
        get => GetValue(HeaderActionProperty);
        set => SetValue(HeaderActionProperty, value);
    }

    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }
}
