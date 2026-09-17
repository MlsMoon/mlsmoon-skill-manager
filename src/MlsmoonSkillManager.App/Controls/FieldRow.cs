using System.Windows;
using System.Windows.Controls;

namespace MlsmoonSkillManager.App.Controls;

public class FieldRow : HeaderedContentControl
{
    static FieldRow() => ThemeChrome.Default<FieldRow>();

    public FieldRow() => ThemeChrome.UseAppStyle(this);

    public static readonly DependencyProperty LabelWidthProperty = DependencyProperty.Register(
        nameof(LabelWidth),
        typeof(GridLength),
        typeof(FieldRow),
        new PropertyMetadata(new GridLength(72)));

    public GridLength LabelWidth
    {
        get => (GridLength)GetValue(LabelWidthProperty);
        set => SetValue(LabelWidthProperty, value);
    }
}
