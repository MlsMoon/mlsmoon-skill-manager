using System.Windows;
using System.Windows.Controls;

namespace MlsmoonSkillManager.App.Controls;

public class StatusLabel : Control
{
    static StatusLabel() => ThemeChrome.Default<StatusLabel>();

    public StatusLabel() => ThemeChrome.UseAppStyle(this);

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(StatusLabel),
        new PropertyMetadata(""));

    public static readonly DependencyProperty ToneProperty = DependencyProperty.Register(
        nameof(Tone),
        typeof(BadgeAppearance),
        typeof(StatusLabel),
        new PropertyMetadata(BadgeAppearance.Neutral));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public BadgeAppearance Tone
    {
        get => (BadgeAppearance)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }
}
