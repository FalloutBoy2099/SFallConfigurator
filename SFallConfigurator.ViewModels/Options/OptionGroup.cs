namespace SFallConfigurator.Options
{
	using System;
	using System.Collections.Generic;
	using Localization;

	/// <summary>
	/// A titled card of options on a settings page. The items are options or notes.
	/// </summary>
	internal sealed class OptionGroup
	{
		private readonly String? _title;

		public OptionGroup(String? title, params Object[] items)
		{
			_title = title;
			Items = items;
		}

		public String? Title => _title is null ? null : Localizer.T(_title);

		internal String? RawTitle => _title;

		public IReadOnlyList<Object> Items { get; }
	}

	/// <summary>
	/// Options that are shown one under the other, in the space of one option of the card.
	/// </summary>
	internal sealed class OptionColumn
	{
		public OptionColumn(IReadOnlyList<ConfigOption> items)
		{
			Items = items;
		}

		public IReadOnlyList<ConfigOption> Items { get; }
	}

	/// <summary>
	/// A paragraph of explanation shown among the options.
	/// </summary>
	internal sealed class OptionNote
	{
		private readonly String _text;

		public OptionNote(String text)
		{
			_text = text;
		}

		public String Text => Localizer.T(_text);

		internal String RawText => _text;
	}
}