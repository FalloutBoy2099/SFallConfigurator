namespace SFallConfigurator.Options
{
	using System;
	using System.Windows.Input;
	using Configuration;
	using Infrastructure;

	/// <summary>
	/// A keyboard key setting (a DirectInput scan code), shown as a button that opens the key picker.
	/// <para>Most key settings also have a check box: unchecked stores 0 (no key), checked stores the key code.</para>
	/// </summary>
	internal sealed class KeyOption : ConfigOption
	{
		private readonly IUserInteraction _interaction;
		private readonly Int32 _defaultCode;

		private Boolean? _isChecked;
		private Int32 _code;

		public KeyOption(OptionLocation location, String key, String label, Int32 defaultCode, IUserInteraction interaction)
			: base(location, key, label)
		{
			_interaction = interaction;
			_defaultCode = defaultCode;
			_code = defaultCode;
			ChooseKeyCommand = new RelayCommand(ChooseKey);

			// The key button depends on the check box and on whether the option is enabled at all.
			PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(IsEnabled))
				{
					OnPropertyChanged(nameof(IsKeyEnabled));
				}
			};
		}

		/// <summary>
		/// False for a setting that is only a key, without the check box.
		/// </summary>
		public Boolean HasToggle { get; init; } = true;

		/// <summary>
		/// Allows the negative codes -1, -2 and -3 that stand for the Ctrl, Alt and Shift modifiers.
		/// </summary>
		public Boolean AllowModifiers { get; init; }

		public ICommand ChooseKeyCommand { get; }

		public Boolean? IsChecked
		{
			get => _isChecked;
			set
			{
				if (_isChecked == value)
				{
					return;
				}

				_isChecked = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(IsKeyEnabled));
				Store();
			}
		}

		public Int32 Code
		{
			get => _code;
			private set
			{
				if (_code == value)
				{
					return;
				}

				_code = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(CodeText));
				Store();
			}
		}

		public String CodeText => DirectInputKeys.GetDisplayName(_code, AllowModifiers);

		/// <summary>
		/// True when the key button can be used.
		/// </summary>
		public Boolean IsKeyEnabled => IsEnabled && (!HasToggle || _isChecked == true);

		protected override void ApplyValue(String? raw)
		{
			if (raw is null)
			{
				IsChecked = null;
				return;
			}

			Int32 value = IniValue.ToInt32(raw);
			if (HasToggle)
			{
				IsChecked = value != 0;
				Code = value != 0 ? value : _defaultCode;
			}
			else
			{
				Code = value;
			}

			OnPropertyChanged(nameof(IsKeyEnabled));
		}

		protected override Object? GetSnapshot()
		{
			return (_isChecked, _code);
		}

		private void Store()
		{
			if (HasToggle && _isChecked is null)
			{
				return;
			}

			Int32 stored = HasToggle && _isChecked == false ? 0 : _code;
			OnValueChanged(IniValue.Format(stored));
		}

		private void ChooseKey()
		{
			Int32? picked = _interaction.PickKeyCode(_code, AllowModifiers);
			if (picked is null)
			{
				return;
			}

			Int32 code = picked.Value;
			Code = !AllowModifiers && code < 0 ? 0 : code;
		}
	}
}