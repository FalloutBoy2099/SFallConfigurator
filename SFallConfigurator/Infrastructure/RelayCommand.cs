namespace SFallConfigurator.Infrastructure
{
	using System;
	using System.Windows.Input;

	/// <summary>
	/// Command that forwards execution to a delegate.
	/// </summary>
	internal sealed class RelayCommand : ICommand
	{
		private readonly Action<Object?> _execute;
		private readonly Predicate<Object?>? _canExecute;

		public RelayCommand(Action execute, Func<Boolean>? canExecute = null)
			: this(_ => execute(), canExecute is null ? null : _ => canExecute())
		{
		}

		public RelayCommand(Action<Object?> execute, Predicate<Object?>? canExecute = null)
		{
			_execute = execute;
			_canExecute = canExecute;
		}

		public event EventHandler? CanExecuteChanged
		{
			add => CommandManager.RequerySuggested += value;
			remove => CommandManager.RequerySuggested -= value;
		}

		public Boolean CanExecute(Object? parameter)
		{
			return _canExecute?.Invoke(parameter) ?? true;
		}

		public void Execute(Object? parameter)
		{
			_execute(parameter);
		}
	}
}