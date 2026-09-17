using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MlsmoonSkillManager.App.Controls;

internal static class ThemeChrome
{
    public static void Default<T>() where T : Control
    {
        Control.BackgroundProperty.OverrideMetadata(typeof(T), new FrameworkPropertyMetadata(Brushes.Transparent));
        Control.BorderBrushProperty.OverrideMetadata(typeof(T), new FrameworkPropertyMetadata(Brushes.Transparent));
    }

    public static void UseAppStyle<T>(T control) where T : FrameworkElement
        => control.SetResourceReference(FrameworkElement.StyleProperty, typeof(T));
}
