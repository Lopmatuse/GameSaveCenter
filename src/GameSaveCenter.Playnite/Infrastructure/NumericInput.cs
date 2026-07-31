using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace GameSaveCenter.Playnite.Infrastructure
{
    /// <summary>Validates integer editor text before a settings binding commits it.</summary>
    public sealed class IntegerRangeValidationRule : ValidationRule
    {
        public int Minimum { get; set; }
        public int Maximum { get; set; } = int.MaxValue;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            var text = Convert.ToString(value, cultureInfo)?.Trim();
            if (!int.TryParse(text, NumberStyles.Integer, cultureInfo, out var parsed))
                return new ValidationResult(false, "请输入整数。");
            if (parsed < Minimum || parsed > Maximum)
                return new ValidationResult(false, $"请输入 {Minimum}–{Maximum} 之间的数值。");
            return ValidationResult.ValidResult;
        }
    }

    /// <summary>Lets numeric fields replace their complete value on keyboard focus without changing mouse caret behavior.</summary>
    public static class SelectAllOnKeyboardFocus
    {
        public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
            "Enabled", typeof(bool), typeof(SelectAllOnKeyboardFocus), new PropertyMetadata(false, OnEnabledChanged));

        public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);
        public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);

        private static void OnEnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
        {
            if (target is not TextBox textBox) return;
            if ((bool)args.NewValue) textBox.GotKeyboardFocus += OnGotKeyboardFocus;
            else textBox.GotKeyboardFocus -= OnGotKeyboardFocus;
        }

        private static void OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs args)
        {
            if (sender is not TextBox textBox) return;
            textBox.Dispatcher.BeginInvoke(new Action(textBox.SelectAll), DispatcherPriority.Input);
        }
    }
}
