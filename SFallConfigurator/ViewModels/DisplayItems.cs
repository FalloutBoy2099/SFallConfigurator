namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Windows.Input;
	using Infrastructure;
	using Localization;

	/// <summary>
	/// A read-only text with a button, for example the name of a file and the button that chooses another one.
	/// </summary>
	internal sealed class FileDisplayItem : ObservableObject
	{
		private String _text = String.Empty;

		private readonly String _label;
		private readonly String? _toolTip;

		public FileDisplayItem(String label, ICommand actionCommand)
		{
			_label = label;
			ActionCommand = actionCommand;
		}

		public String Label => Localizer.T(_label);

		public ICommand ActionCommand { get; }

		public String ActionLabel { get; init; } = "...";

		public String? ToolTip
		{
			get => _toolTip is null ? null : Localizer.T(_toolTip);
			init => _toolTip = value;
		}

		public String Text
		{
			get => _text;
			set => SetProperty(ref _text, value);
		}
	}

	/// <summary>
	/// A button placed among the options.
	/// </summary>
	internal sealed class CommandItem
	{
		private readonly String _label;
		private readonly String? _toolTip;

		public CommandItem(String label, ICommand command)
		{
			_label = label;
			Command = command;
		}

		public String Label => Localizer.T(_label);

		public ICommand Command { get; }

		public String? ToolTip
		{
			get => _toolTip is null ? null : Localizer.T(_toolTip);
			init => _toolTip = value;
		}
	}

	/// <summary>
	/// A check box whose state is not stored in an ini file: a handler applies the change.
	/// A null state means the state is unknown and the check box is disabled.
	/// </summary>
	internal sealed class ToggleItem : ObservableObject
	{
		private readonly Func<Boolean, Boolean> _apply;
		private readonly String _label;
		private readonly String? _toolTip;
		private Boolean? _isChecked;
		private Boolean _isRestoring;

		/// <param name="apply">Applies the new state. Returns false when it failed; the check box is then restored.</param>
		public ToggleItem(String label, Func<Boolean, Boolean> apply)
		{
			_label = label;
			_apply = apply;
		}

		public String Label => Localizer.T(_label);

		public String? ToolTip
		{
			get => _toolTip is null ? null : Localizer.T(_toolTip);
			init => _toolTip = value;
		}

		public Boolean IsEnabled => _isChecked.HasValue;

		public Boolean? IsChecked
		{
			get => _isChecked;
			set
			{
				if (_isChecked == value)
				{
					return;
				}

				Boolean? previous = _isChecked;
				_isChecked = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(IsEnabled));

				if (!_isRestoring && value.HasValue && previous.HasValue && !_apply(value.Value))
				{
					_isRestoring = true;
					try
					{
						IsChecked = previous;
					}
					finally
					{
						_isRestoring = false;
					}
				}
			}
		}

		/// <summary>
		/// Shows the real state without applying anything.
		/// </summary>
		public void Show(Boolean? state)
		{
			_isRestoring = true;
			try
			{
				IsChecked = state;
			}
			finally
			{
				_isRestoring = false;
			}
		}
	}
}