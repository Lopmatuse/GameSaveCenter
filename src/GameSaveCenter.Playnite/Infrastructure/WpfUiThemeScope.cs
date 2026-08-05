using System.Windows;

namespace GameSaveCenter.Playnite.Infrastructure
{
    /// <summary>
    /// Compatibility hook retained for the existing theme pipeline. GameSaveCenter now
    /// uses native WPF controls exclusively, so there is no third-party theme scope to apply.
    /// </summary>
    public static class WpfUiThemeScope
    {
        public static void Apply(ResourceDictionary viewResources, bool isDark)
        {
            // Intentionally empty. Keeping the method avoids changing all existing view
            // lifecycle code while eliminating third-party theme/resource side effects.
        }
    }
}
