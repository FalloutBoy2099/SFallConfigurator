namespace SFallConfigurator.Options
{
	using System;
	using Configuration;
	using Localization;

	/// <summary>
	/// An on/off setting, shown as a check box.
	/// </summary>
	internal sealed class BoolOption : ConfigOption
	{
		private Boolean? _value;

		public BoolOption(OptionLocation location, String key, String label)
			: base(location, key, label)
		{
		}

		/// <summary>
		/// Ini text stored when the option is switched on.
		/// </summary>
		public String TrueValue { get; set; } = "1";

		/// <summary>
		/// Computes the text stored when the option is switched on, for a value that depends on another option.
		/// </summary>
		public Func<String>? TrueValueSource { get; set; }

		public String FalseValue { get; set; } = "0";

		/// <summary>
		/// Decides whether a raw value means "on". By default any non-zero number does.
		/// </summary>
		public Func<String, Boolean>? Interpret { get; set; }

		/// <summary>
		/// The state; null while the option is unavailable (shown as an indeterminate check box).
		/// </summary>
		public Boolean? Value
		{
			get => _value;
			set
			{
				if (_value == value)
				{
					return;
				}

				_value = value;
				OnPropertyChanged();
				OnValueChanged(value is null ? null : value.Value ? TrueValueSource?.Invoke() ?? TrueValue : FalseValue);
			}
		}

		protected override void ApplyValue(String? raw)
		{
			Value = raw is null ? null : Interpret?.Invoke(raw) ?? (IniValue.ToInt32(raw) != 0);
		}

		protected override Object? GetSnapshot()
		{
			return _value;
		}

		internal override String InitialRaw => KnownDefault ?? FalseValue;

		internal override String DescribeRaw(String raw)
		{
			return Localizer.T(Interpret?.Invoke(raw) ?? (IniValue.ToInt32(raw) != 0) ? "Enabled" : "Disabled");
		}
	}
}