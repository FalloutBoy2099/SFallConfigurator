namespace SFallConfigurator.Options
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Configuration;
	using Localization;

	/// <summary>
	/// How a <see cref="ChoiceOption"/> is shown.
	/// </summary>
	internal enum ChoicePresentation
	{
		DropDown,
		RadioButtons,
	}

	/// <summary>
	/// A setting with a fixed list of choices, shown as a drop-down list or as radio buttons.
	/// </summary>
	internal class ChoiceOption : ConfigOption
	{
		private Int32 _selectedIndex = -1;

		public ChoiceOption(OptionLocation location, String key, String label, IReadOnlyList<String> items)
			: base(location, key, label)
		{
			Items = items.Select(Localizer.T).ToList();
		}

		public IReadOnlyList<String> Items { get; }

		public ChoicePresentation Presentation { get; init; }

		/// <summary>
		/// The ini value of each item. When missing, the value of an item is its index.
		/// </summary>
		public IReadOnlyList<String>? Values { get; init; }

		/// <summary>
		/// Maps a raw ini value to an item index, when the value is not simply the index.
		/// </summary>
		public Func<String, Int32>? IndexFromRaw { get; init; }

		public Int32 SelectedIndex
		{
			get => _selectedIndex;
			set
			{
				if (_selectedIndex == value)
				{
					return;
				}

				_selectedIndex = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(SelectedValue));
				OnValueChanged(value < 0 ? null : GetRawValue(value));
				if (value >= 0 && !IsLoading && IsAvailable)
				{
					OnIndexStored(value);
				}
			}
		}

		/// <summary>
		/// The ini value of the selected item, or null when nothing is selected.
		/// </summary>
		public String? SelectedValue => _selectedIndex < 0 ? null : GetRawValue(_selectedIndex);

		protected override void ApplyValue(String? raw)
		{
			SelectedIndex = raw is null ? -1 : FindIndex(raw);
		}

		protected override Object? GetSnapshot()
		{
			return _selectedIndex;
		}

		/// <summary>
		/// Maps a raw ini value to an item index.
		/// </summary>
		protected virtual Int32 FindIndex(String raw)
		{
			if (IndexFromRaw is not null)
			{
				return Math.Clamp(IndexFromRaw(raw), 0, Items.Count - 1);
			}

			Int32 number = IniValue.ToInt32(raw, Int32.MinValue);
			if (Values is null)
			{
				return number >= 0 && number < Items.Count ? number : 0;
			}

			for (Int32 index = 0; index < Values.Count; index++)
			{
				if (IniValue.ToInt32(Values[index], Int32.MaxValue) == number)
				{
					return index;
				}
			}

			return 0;
		}

		/// <summary>
		/// Called after the value of the selected item was stored, to store related keys.
		/// </summary>
		protected virtual void OnIndexStored(Int32 index)
		{
		}

		private String GetRawValue(Int32 index)
		{
			return Values is not null && index < Values.Count ? Values[index] : IniValue.Format(index);
		}
	}
}