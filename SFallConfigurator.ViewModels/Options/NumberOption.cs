namespace SFallConfigurator.Options
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows.Input;
	using Configuration;
	using Infrastructure;
	using Localization;

	/// <summary>
	/// How a <see cref="NumberOption"/> is shown.
	/// </summary>
	internal enum NumberPresentation
	{
		SpinBox,
		Slider,

		/// <summary>
		/// The number is the index of a color of the palette of the game: a button shows the color and opens the palette.
		/// </summary>
		Palette,
	}

	/// <summary>
	/// A color of the palette, to choose.
	/// </summary>
	internal sealed class PaletteEntry
	{
		public PaletteEntry(Int32 index, Int32 rgb, ICommand select)
		{
			Index = index;
			Rgb = rgb;
			SelectCommand = select;
		}

		public Int32 Index { get; }

		/// <summary>
		/// The color as 0xRRGGBB.
		/// </summary>
		public Int32 Rgb { get; }

		public ICommand SelectCommand { get; }
	}

	/// <summary>
	/// An integer setting with a range.
	/// </summary>
	internal sealed class NumberOption : ConfigOption
	{
		private Int32 _value;
		private String? _minimumLabel;
		private String? _maximumLabel;
		private Boolean _isPaletteOpen;
		private IReadOnlyList<PaletteEntry>? _paletteEntries;

		public NumberOption(OptionLocation location, String key, String label, Int32 minimum, Int32 maximum)
			: base(location, key, label)
		{
			Minimum = minimum;
			Maximum = maximum;
			_value = Math.Clamp(0, minimum, maximum);
		}

		public Int32 Minimum { get; }

		public Int32 Maximum { get; }

		public Int32 Increment { get; set; } = 1;

		public NumberPresentation Presentation { get; set; }

		/// <summary>
		/// Gives the colors of the palette of the game (as 0xRRGGBB), or null when there is none. Read when the colors are first shown.
		/// </summary>
		public Func<IReadOnlyList<Int32>?>? PaletteSource { get; set; }

		/// <summary>
		/// The colors to choose from; empty when the game has no palette.
		/// </summary>
		public IReadOnlyList<PaletteEntry> PaletteEntries
		{
			get
			{
				_paletteEntries ??= (PaletteSource?.Invoke() ?? []).Select((rgb, index) => new PaletteEntry(index, rgb, new RelayCommand(() => ChoosePaletteColor(index)))).ToList();
				return _paletteEntries;
			}
		}

		/// <summary>
		/// True when the palette can be shown.
		/// </summary>
		public Boolean HasPalette => PaletteEntries.Count > 0;

		/// <summary>
		/// The color of the number, as 0xRRGGBB; the gray of the same number when there is no palette.
		/// </summary>
		public Int32 ColorRgb => HasPalette && _value >= 0 && _value < PaletteEntries.Count ? PaletteEntries[_value].Rgb : 0x808080;

		public Boolean IsPaletteOpen
		{
			get => _isPaletteOpen;
			set => SetProperty(ref _isPaletteOpen, value);
		}

		/// <summary>
		/// Captions at both ends of a slider.
		/// </summary>
		public String? MinimumLabel
		{
			get => _minimumLabel is null ? null : Localizer.T(_minimumLabel);
			set => _minimumLabel = value;
		}

		public String? MaximumLabel
		{
			get => _maximumLabel is null ? null : Localizer.T(_maximumLabel);
			set => _maximumLabel = value;
		}

		/// <summary>
		/// Adjusts a value entered by the user. Receives the new and the previous value.
		/// </summary>
		public Func<Int32, Int32, Int32>? Normalize { get; set; }

		/// <summary>
		/// Converts the raw ini text to the number, when it is not a plain integer.
		/// </summary>
		public Func<String, Int32>? ParseRaw { get; set; }

		/// <summary>
		/// Converts the number to the raw ini text, when it is not a plain integer.
		/// </summary>
		public Func<Int32, String>? FormatRaw { get; set; }

		/// <summary>
		/// Text shown next to a slider.
		/// </summary>
		public Func<Int32, String>? DisplayText { get; set; }

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
				OnPropertyChanged(nameof(ColorRgb));
				OnValueChanged(FormatRaw?.Invoke(adjusted) ?? IniValue.Format(adjusted));
			}
		}

		internal override String InitialRaw
		{
			get
			{
				Int32 start = Math.Clamp(0, Minimum, Maximum);
				return KnownDefault ?? FormatRaw?.Invoke(start) ?? IniValue.Format(start);
			}
		}

		private void ChoosePaletteColor(Int32 index)
		{
			Value = index;
			IsPaletteOpen = false;
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