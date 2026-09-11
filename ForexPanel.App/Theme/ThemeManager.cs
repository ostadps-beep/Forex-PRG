using Microsoft.Win32;
using System.Windows;

namespace ForexPanel.App.Theme;

public static class ThemeManager
{
    private static ResourceDictionary? _currentTheme;

    public static ThemeMode CurrentMode { get; private set; } = ThemeMode.System;

    public static void Apply(ThemeMode mode)
    {
        CurrentMode = mode;

        var effectiveMode = mode == ThemeMode.System
            ? DetectWindowsTheme()
            : mode;

        var uri = effectiveMode == ThemeMode.Dark
            ? new Uri("/ForexPanel.App;component/Theme/DarkTheme.xaml", UriKind.Relative)
            : new Uri("/ForexPanel.App;component/Theme/LightTheme.xaml", UriKind.Relative);

        var theme = new ResourceDictionary { Source = uri };
        var resources = Application.Current.Resources;

        if (_currentTheme != null)
            resources.MergedDictionaries.Remove(_currentTheme);

        resources.MergedDictionaries.Add(theme);
        _currentTheme = theme;
    }

    public static ThemeMode DetectWindowsTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            var value = key?.GetValue("AppsUseLightTheme");

            if (value is int intValue)
                return intValue == 0 ? ThemeMode.Dark : ThemeMode.Light;
        }
        catch
        {
            // Fall back to light if the Windows theme cannot be read.
        }

        return ThemeMode.Light;
    }
}
