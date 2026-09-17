using System.Windows;
using Microsoft.Win32;
using MlsmoonSkillManager.Core.Services;

namespace MlsmoonSkillManager.App.Theming;

public static class ThemeManager
{
    public static string Preference { get; private set; } = ThemeResolver.System;
    public static string Effective { get; private set; } = ThemeResolver.Dark;
    public static event EventHandler? Changed;

    public static void Apply(string? preference)
    {
        Preference = ThemeResolver.Normalize(preference);
        Effective = ThemeResolver.Resolve(Preference, WindowsAppsUseLightTheme());
        var source = new Uri($"Themes/{Effective}.xaml", UriKind.Relative);
        var dict = new ResourceDictionary { Source = source };
        var merged = Application.Current.Resources.MergedDictionaries;
        for (var i = merged.Count - 1; i >= 0; i--)
        {
            var path = merged[i].Source?.OriginalString ?? "";
            if (path.Contains("Themes/Dark.xaml", StringComparison.OrdinalIgnoreCase)
                || path.Contains("Themes/Light.xaml", StringComparison.OrdinalIgnoreCase))
            {
                merged.RemoveAt(i);
            }
        }

        merged.Insert(0, dict);
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static void WatchSystem()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public static bool WindowsAppsUseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is not int number || number != 0;
        }
        catch
        {
            return true;
        }
    }

    private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (Preference != ThemeResolver.System)
        {
            return;
        }

        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color)
        {
            var app = Application.Current;
            if (app is null)
            {
                return;
            }

            app.Dispatcher.Invoke(() => Apply(Preference));
        }
    }
}
