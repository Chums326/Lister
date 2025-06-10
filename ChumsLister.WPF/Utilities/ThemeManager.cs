using System;
using System.Linq;
using System.Windows;
using System.Diagnostics;

namespace ChumsLister.WPF.Utilities
{
    public static class ThemeManager
    {
        public static void ApplyTheme(bool useDarkMode)
        {
            try
            {
                var app = System.Windows.Application.Current;

                if (app == null) return;

                // 1. Clear only theme dictionaries (not Styles.xaml)
                var toRemove = app.Resources.MergedDictionaries
                    .Where(d => d.Source != null &&
                                (d.Source.ToString().Contains("LightTheme") ||
                                 d.Source.ToString().Contains("DarkTheme") ||
                                 d.Source.ToString().Contains("DarkThemeOverride")))
                    .ToList();

                foreach (var dict in toRemove)
                {
                    Debug.WriteLine($"Removing dictionary: {dict.Source}");
                    app.Resources.MergedDictionaries.Remove(dict);
                }

                // 2. Make sure Styles.xaml is present
                bool hasStyles = app.Resources.MergedDictionaries
                    .Any(d => d.Source != null && d.Source.ToString().Contains("Styles.xaml"));

                if (!hasStyles)
                {
                    app.Resources.MergedDictionaries.Insert(0,
                        new ResourceDictionary { Source = new Uri("/Resources/Styles.xaml", UriKind.Relative) });
                }

                // 3. Load base theme
                string themePath = useDarkMode ? "/Resources/DarkTheme.xaml" : "/Resources/LightTheme.xaml";
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) });

                // 4. Optional: Dark mode override
                if (useDarkMode)
                {
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("/Resources/DarkThemeOverride.xaml", UriKind.Relative)
                    });
                }

                app.Resources["IsDarkTheme"] = useDarkMode;

                // 5. Refresh visuals
                foreach (Window window in app.Windows)
                {
                    if (window.IsLoaded)
                    {
                        foreach (var child in LogicalTreeHelper.GetChildren(window))
                        {
                            if (child is FrameworkElement element)
                                element.InvalidateVisual();
                        }

                        window.InvalidateVisual();
                    }
                }

                Debug.WriteLine($"Theme applied: {(useDarkMode ? "Dark" : "Light")}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying theme: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
            }
        }

    }
}