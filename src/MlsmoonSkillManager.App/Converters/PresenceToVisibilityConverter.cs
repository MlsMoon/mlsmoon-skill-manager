using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MlsmoonSkillManager.App.Converters;

public sealed class PresenceToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return Visibility.Collapsed;
        }

        if (value is string text && string.IsNullOrWhiteSpace(text))
        {
            return Visibility.Collapsed;
        }

        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
