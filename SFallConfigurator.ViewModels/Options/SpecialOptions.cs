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
	/// How the Hi-Res patch opens the game window. Stored in two keys:
	/// WINDOWED, and WINDOWED_FULLSCREEN which only matters for windowed mode.
	/// </summary>
	internal sealed class WindowModeOption : ChoiceOption
	{
		private const String FullWindowedKey = "WINDOWED_FULLSCREEN";

		public WindowModeOption(OptionLocation location, String label)
			: base(location, "WINDOWED", label, ["Full Screen", "Windowed", "Full Windowed"])
		{
			Values = ["0", "1", "1"];
			DefaultValue = "0";
		}

		protected override Int32 FindIndex(String raw)
		{
			if (IniValue.ToInt32(raw) == 0)
			{
				return 0;
			}

			return IniValue.ToInt32(ReadOtherKey(FullWindowedKey)) != 0 ? 2 : 1;
		}

		protected override void OnIndexStored(Int32 index)
		{
			WriteOtherKey(FullWindowedKey, index == 2 ? "1" : "0");
		}
	}

	/// <summary>
	/// The picture of the side panels of the Hi-Res interface bar (IFACE_BAR_SIDE_ART): 0 to 99,
	/// where 0 to 2 are the pictures shipped with the patch.
	/// </summary>
	internal sealed class BarArtOption : ConfigOption
	{
		private const Int32 MaximumArt = 99;
		private const Int32 UnknownPreset = 3;

		private Int32 _art;

		public BarArtOption(OptionLocation location, String key, String label)
			: base(location, key, label)
		{
			DefaultValue = "1";
			Presets = Presets.Select(Localizer.T).ToList();
			IncrementCommand = new RelayCommand(() => Art++, () => IsEnabled && _art < MaximumArt);
			DecrementCommand = new RelayCommand(() => Art--, () => IsEnabled && _art > 0);
		}

		public IReadOnlyList<String> Presets { get; } = ["0: Black (None)", "1: Metal", "2: Leather", "[Unknown]"];

		public ICommand IncrementCommand { get; }

		public ICommand DecrementCommand { get; }

		public Int32 Art
		{
			get => _art;
			set
			{
				Int32 art = Math.Clamp(value, 0, MaximumArt);
				if (_art == art)
				{
					OnPropertyChanged();
					return;
				}

				_art = art;
				OnPropertyChanged();
				OnPropertyChanged(nameof(PresetIndex));
				OnPropertyChanged(nameof(PreviewImage));
				OnValueChanged(IniValue.Format(art));
			}
		}

		/// <summary>
		/// Index in <see cref="Presets"/>: the picture itself for 0 to 2, "[Unknown]" for the others.
		/// </summary>
		public Int32 PresetIndex
		{
			get => _art is >= 0 and < UnknownPreset ? _art : UnknownPreset;
			set
			{
				if (value is >= 0 and < UnknownPreset)
				{
					Art = value;
				}
				else
				{
					// "[Unknown]" is only what is shown for another picture: the list goes back to what is selected.
					OnPropertyChanged();
				}
			}
		}

		public String PreviewImage => _art switch
		{
			1 => "pack://application:,,,/Resources/Metal.png",
			2 => "pack://application:,,,/Resources/Leather.png",
			_ => "pack://application:,,,/Resources/Black.png",
		};

		protected override void ApplyValue(String? raw)
		{
			Art = IniValue.ToInt32(raw, 1);
		}

		protected override Object? GetSnapshot()
		{
			return _art;
		}
	}
}