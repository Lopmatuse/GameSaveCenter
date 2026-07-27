using System;
using System.Windows.Input;

namespace GameSaveCenter.Playnite.ViewModels
{
    /// <summary>
    /// WPF command that participates in CommandManager requery so selection changes
    /// immediately refresh button enabled states.
    /// </summary>
    public sealed class RelayCommand : ICommand
    {
        private readonly Action<object?> execute;
        private readonly Predicate<object?>? canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => canExecute == null || canExecute(parameter);
        public void Execute(object? parameter) => execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
    }
}
