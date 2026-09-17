using System.Windows;
using System.Windows.Controls;

namespace MlsmoonSkillManager.App.Controls;

public class Badge : ContentControl
{
    static Badge() => ThemeChrome.Default<Badge>();

    public Badge() => ThemeChrome.UseAppStyle(this);

    public static readonly DependencyProperty AppearanceProperty = DependencyProperty.Register(
        nameof(Appearance),
        typeof(BadgeAppearance),
        typeof(Badge),
        new PropertyMetadata(BadgeAppearance.Accent));

    public BadgeAppearance Appearance
    {
        get => (BadgeAppearance)GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }
}
