namespace SFallConfigurator.Options
{
	using System;
	using Configuration;
	using Localization;

	/// <summary>
	/// How a <see cref="NumberOption"/> is shown.
	/// </summary>
	internal enum NumberPresentation
	{
		SpinBox,
		Slider,
	}

	/// <summary>
	/// An integer setting with a range.
	/// </summary>
	internal sealed class NumberOption : ConfigOption
	{
		private Int32 _value;
		private String? _minimumLabel;
		private String? _maximumLabel;

		public NumberOption(OptionLocation location, String key, String label, Int32 minimum, Int32 maximum)
			: base(location, key, label)
		{
			Minimum = minimum;
			Maximum = maximum;
			_value = Math.Clamp(0, minimum, maximum);
		}

		public Int32 Minimum { get; }

		public Int32 Maximum { get; }

		public Int32 Increment { get; init; } = 1;

		public NumberPresentation Presentation { get; init; }

		/// <summary>
		/// Captions at both ends of a slider.
		/// </summary>
		public String? MinimumLabel
		{
			get => _minimumLabel is null ? null : Localizer.T(_minimumLabel);
			init => _minimumLabel = value;
		}

		public String? MaximumLabel
		{
			get => _maximumLabel is null ? null : Localizer.T(_maximumLabel);
			init => _maximumLabel = value;
		}

		/// <summary>
		/// Adjusts a value entered by the user. Receives the new and the previous value.
		/// </summary>
		public Func<Int32, Int32, Int32>? Normalize { get; init; }

		/// <summary>
		/// Converts the raw ini text to the number, when it is not a plain integer.
		/// </summary>
		public Func<String, Int32>? ParseRaw { get; init; }

		/// <summary>
		/// Converts the number to the raw ini text, when it is not a plain integer.
		/// </summary>
		public Func<Int32, String>? FormatRaw { get; init; }

		/// <summary>
		/// Text shown next to a slider.
		/// </summary>
		public Func<Int32, String>? DisplayText { get; init; }

		public String ValueText => DisplayText?.Invoke(_value) ?? IniValue.Format(_value);

		public Int32 Value
		{
			get => _value;
			set
			{
				Int32 adjusted = Math.Clamp(Normalize is null || IsLoading ? value : Normalize(value, _value), Minimum, Maximum);
				if (adjusted == _value)
				{
					// The bound control may show text that was rejected; make it show the real value again.
					OnPropertyChanged();
					return;
				}

				_value = adjusted;
				OnPropertyChanged();
				OnPropertyChanged(nameof(ValueText));
				OnValueChanged(FormatRaw?.Invoke(adjusted) ?? IniValue.Format(adjusted));
			}
		}

		protected override void ApplyValue(String? raw)
		{
			Int32 parsed = raw is null ? Math.Clamp(0, Minimum, Maximum) : ParseRaw?.Invoke(raw) ?? IniValue.ToInt32(raw, _value);
			Value = parsed;
		}

		protected override Object? GetSnapshot()
		{
			return _value;
		}
	}
}