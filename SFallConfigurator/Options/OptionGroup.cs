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

		public IReadOnlyList<Object> Items { get; }
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
	}
}