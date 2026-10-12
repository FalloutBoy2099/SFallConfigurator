namespace SFallConfigurator.Infrastructure
{
	using System;
	using System.Collections.Generic;
	using System.Windows.Input;

	/// <summary>
	/// Command that forwards execution to a delegate.
	/// <para>
	/// A command tells its buttons to ask again whether it can run with <see cref="RaiseCanExecuteChanged"/>. The window
	/// does it for every command when the state of the interface may have changed (<see cref="RaiseAll"/>), so that this
	/// project does not need the command manager of WPF.
	/// </para>
	/// </summary>
	internal sealed class RelayCommand : ICommand
	{
		private static readonly List<WeakReference<RelayCommand>> Commands = [];

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

			lock (Commands)
			{
				Commands.Add(new WeakReference<RelayCommand>(this));
			}
		}

		public event EventHandler? CanExecuteChanged;

		public Boolean CanExecute(Object? parameter)
		{
			return _canExecute?.Invoke(parameter) ?? true;
		}

		public void Execute(Object? parameter)
		{
			_execute(parameter);
		}

		public void RaiseCanExecuteChanged()
		{
			CanExecuteChanged?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// Asks every command that is still alive to tell its buttons to ask again.
		/// </summary>
		public static void RaiseAll()
		{
			List<RelayCommand> alive = [];
			lock (Commands)
			{
				Commands.RemoveAll(reference => !reference.TryGetTarget(out _));
				foreach (WeakReference<RelayCommand> reference in Commands)
				{
					if (reference.TryGetTarget(out RelayCommand? command))
					{
						alive.Add(command);
					}
				}
			}

			alive.ForEach(command => command.RaiseCanExecuteChanged());
		}
	}
}