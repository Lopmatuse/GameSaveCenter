using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GameSaveCenter.Playnite.Views
{
    /// <summary>
    /// Creates a dependency-light fallback UserControl for Playnite host failures.
    /// It deliberately avoids the plugin resource dictionaries so a XAML/resource regression
    /// cannot recursively fail while displaying its own diagnostic message.
    /// </summary>
    internal static class SafeViewFactory
    {
        public static UserControl Create(string title, string message, Exception exception)
        {
            var details = exception == null ? string.Empty : exception.GetType().Name + ": " + exception.Message;
            var panel = new StackPanel
            {
                Margin = new Thickness(28),
                VerticalAlignment = VerticalAlignment.Top
            };

            panel.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(new TextBlock
            {
                Text = message,
                Margin = new Thickness(0, 12, 0, 0),
                FontSize = 14,
                Foreground = Brushes.Gainsboro,
                TextWrapping = TextWrapping.Wrap
            });
            if (!string.IsNullOrWhiteSpace(details))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = details,
                    Margin = new Thickness(0, 12, 0, 0),
                    FontSize = 12,
                    Foreground = Brushes.Silver,
                    TextWrapping = TextWrapping.Wrap,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxHeight = 56
                });
            }

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 30, 38)),
                Child = panel
            };
            return new UserControl { Content = border };
        }
    }
}
