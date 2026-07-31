using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Markup;

namespace GameSaveCenter.Playnite.Infrastructure
{
    /// <summary>
    /// Applies WPF-UI resources only inside one GameSaveCenter view. Playnite owns
    /// application-level resources, so this helper deliberately never touches them.
    /// </summary>
    public static class WpfUiThemeScope
    {
        public static void Apply(ResourceDictionary viewResources, bool isDark)
        {
            if (viewResources == null) return;
            var theme = SystemParameters.HighContrast
                ? ApplicationTheme.HighContrast
                : isDark ? ApplicationTheme.Dark : ApplicationTheme.Light;
            ApplyMergedDictionaries(viewResources, theme);
        }

        private static void ApplyMergedDictionaries(ResourceDictionary dictionary, ApplicationTheme theme)
        {
            foreach (var merged in dictionary.MergedDictionaries)
            {
                if (merged is ThemesDictionary wpfUiThemes)
                {
                    wpfUiThemes.Theme = theme;
                }

                ApplyMergedDictionaries(merged, theme);
            }
        }
    }
}
