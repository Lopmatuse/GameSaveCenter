using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace GameSaveCenter.Playnite.Controls
{
    /// <summary>
    /// Small native WPF compatibility controls used by the existing XAML vocabulary.
    /// They intentionally contain no theme or application-resource side effects.
    /// </summary>
    public class Button : System.Windows.Controls.Button
    {
        static Button()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Button), new FrameworkPropertyMetadata(typeof(System.Windows.Controls.Button)));
        }

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.RegisterAttached("CornerRadius", typeof(CornerRadius), typeof(Button), new FrameworkPropertyMetadata(new CornerRadius(0)));

        public static void SetCornerRadius(DependencyObject element, CornerRadius value) => element.SetValue(CornerRadiusProperty, value);
        public static CornerRadius GetCornerRadius(DependencyObject element) => (CornerRadius)element.GetValue(CornerRadiusProperty);

        public static readonly DependencyProperty AppearanceProperty =
            DependencyProperty.Register("Appearance", typeof(string), typeof(Button), new PropertyMetadata("Secondary"));

        public string Appearance
        {
            get => (string)GetValue(AppearanceProperty);
            set => SetValue(AppearanceProperty, value);
        }

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register("Icon", typeof(object), typeof(Button), new PropertyMetadata(null));

        public object Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }
    }

    public class Card : ContentControl
    {
        static Card()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(Card), new FrameworkPropertyMetadata(typeof(ContentControl)));
        }
    }

    public class ToggleSwitch : CheckBox
    {
        static ToggleSwitch()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ToggleSwitch), new FrameworkPropertyMetadata(typeof(CheckBox)));
        }

        public static readonly DependencyProperty OnContentProperty =
            DependencyProperty.Register("OnContent", typeof(object), typeof(ToggleSwitch), new PropertyMetadata(null));

        public object OnContent
        {
            get => GetValue(OnContentProperty);
            set => SetValue(OnContentProperty, value);
        }

        public static readonly DependencyProperty OffContentProperty =
            DependencyProperty.Register("OffContent", typeof(object), typeof(ToggleSwitch), new PropertyMetadata(null));

        public object OffContent
        {
            get => GetValue(OffContentProperty);
            set => SetValue(OffContentProperty, value);
        }
    }

    public class NumberBox : TextBox
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(NumberBox), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register("Minimum", typeof(double), typeof(NumberBox), new PropertyMetadata(double.MinValue));
        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(double), typeof(NumberBox), new PropertyMetadata(double.MaxValue));
        public static readonly DependencyProperty MaxDecimalPlacesProperty =
            DependencyProperty.Register("MaxDecimalPlaces", typeof(int), typeof(NumberBox), new PropertyMetadata(2));

        public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
        public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
        public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
        public int MaxDecimalPlaces { get => (int)GetValue(MaxDecimalPlacesProperty); set => SetValue(MaxDecimalPlacesProperty, value); }
    }

    public class ProgressRing : ProgressBar
    {
        static ProgressRing()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ProgressRing), new FrameworkPropertyMetadata(typeof(ProgressBar)));
        }
    }

    public class SymbolIcon : TextBlock
    {
        public static readonly DependencyProperty SymbolProperty =
            DependencyProperty.Register("Symbol", typeof(object), typeof(SymbolIcon), new PropertyMetadata(null));

        public object Symbol
        {
            get => GetValue(SymbolProperty);
            set => SetValue(SymbolProperty, value);
        }
    }

    public class SnackbarPresenter : ContentControl
    {
        static SnackbarPresenter()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SnackbarPresenter), new FrameworkPropertyMetadata(typeof(ContentControl)));
        }
    }

    public sealed class Snackbar
    {
        private readonly SnackbarPresenter presenter;
        private DispatcherTimer? timer;

        public Snackbar(SnackbarPresenter presenter)
        {
            this.presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
        }

        public object? Title { get; set; }
        public object? Content { get; set; }
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);
        public bool IsCloseButtonEnabled { get; set; }

        public void Show()
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical };
            panel.Children.Add(new TextBlock { Text = Title == null ? string.Empty : Title.ToString(), FontWeight = FontWeights.SemiBold });
            panel.Children.Add(new TextBlock { Text = Content == null ? string.Empty : Content.ToString(), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
            presenter.Content = panel;
            if (Timeout > TimeSpan.Zero)
            {
                timer?.Stop();
                timer = new DispatcherTimer { Interval = Timeout };
                timer.Tick += (sender, args) =>
                {
                    timer.Stop();
                    presenter.Content = null;
                };
                timer.Start();
            }
        }
    }
}
