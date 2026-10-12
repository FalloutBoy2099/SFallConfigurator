namespace SFallConfigurator.Infrastructure
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// A setting that is about to change, as the user sees it.
	/// </summary>
	internal sealed record ChangeRow(String Name, String Before, String After, String Location);

	/// <summary>
	/// The changes of one file.
	/// </summary>
	internal sealed record ChangeGroup(String FileName, IReadOnlyList<ChangeRow> Rows);

	/// <summary>
	/// What saving would write: the settings that differ from the saved files, file by file, named like the options.
	/// </summary>
	internal sealed class ChangePreview
	{
		public ChangePreview(IReadOnlyList<ChangeGroup> groups)
		{
			Groups = groups;
		}

		public IReadOnlyList<ChangeGroup> Groups { get; }

		public Boolean IsEmpty => Groups.Count == 0;

		public Int32 Count => Groups.Sum(group => group.Rows.Count);
	}
}