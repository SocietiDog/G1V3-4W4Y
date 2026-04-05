using System;
using System.Windows.Input;

namespace Gw2Giveaway.Helpers
{
    /// <summary>
    /// Non-generic RelayCommand with overloads for parameterless and parameterized usage.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        // Parameterized version
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        // Parameterless overload (most common in your code)
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
            : this(
                execute == null ? throw new ArgumentNullException(nameof(execute)) : _ => execute(),
                canExecute == null ? null : _ => canExecute())
        {
        }

        public bool CanExecute(object? parameter)
            => _canExecute == null || _canExecute(parameter);

        public void Execute(object? parameter)
            => _execute(parameter);

        // Automatic WPF requery support
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }

    /// <summary>
    /// Generic RelayCommand<T> for strongly-typed parameters.
    /// Safely handles wrong parameter types (CanExecute false, Execute no-op).
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool>? _canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
            => parameter is T t && (_canExecute?.Invoke(t) ?? true);

        public void Execute(object? parameter)
        {
            if (parameter is T t)
                _execute(t);
        }

        // Automatic WPF requery support
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}