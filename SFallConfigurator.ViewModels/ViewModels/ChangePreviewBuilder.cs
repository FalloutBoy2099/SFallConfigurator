namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Configuration;
	using Infrastructure;
	using Localization;
	using Options;

	/// <summary>
	/// Builds the preview of what saving would write: the settings get the names and the texts of the options.
	/// </summary>
	internal static class ChangePreviewBuilder
	{
		/// <summary>
		/// <paramref name="options"/> give the names and the way a value is shown.
		/// </summary>
		public static ChangePreview Create(IReadOnlyList<FileChanges> files, IEnumerable<ConfigOption> options, IReadOnlyList<String> savedOrder, IReadOnlyList<String> currentOrder)
		{
			List<ConfigOption> known = options.ToList();
			List<ChangeGroup> groups = [];
			foreach (FileChanges file in files)
			{
				groups.Add(new ChangeGroup(file.FileName, file.Changes.Select(change => CreateRow(file.FileName, change, known)).ToList()));
			}

			if (!savedOrder.SequenceEqual(currentOrder))
			{
				ChangeRow order = new(Localizer.T("Load order of the mods"), Join(savedOrder), Join(currentOrder), Path.Combine("mods", "mods_order.ini"));
				groups.Add(new ChangeGroup(Path.Combine("mods", "mods_order.ini"), [order]));
			}

			return new ChangePreview(groups);
		}

		private static String Join(IReadOnlyList<String> lines)
		{
			return lines.Count == 0 ? Localizer.T("(none)") : String.Join(", ", lines);
		}

		private static ChangeRow CreateRow(String fileName, IniChange change, IReadOnlyList<ConfigOption> options)
		{
			ConfigOption? option = options.FirstOrDefault(candidate => Matches(candidate, fileName, change));
			String location = change.Section is null ? change.Key : "[" + change.Section + "] " + change.Key;
			String name = option is null ? location : option.PlainLabel;
			return new ChangeRow(name, Show(option, change.Before, "(not set)"), Show(option, change.After, "(removed)"), location);
		}

		private static Boolean Matches(ConfigOption option, String fileName, IniChange change)
		{
			return option.Key.Equals(change.Key, StringComparison.OrdinalIgnoreCase)
				&& (option.Section is null || option.Section.Equals(change.Section, StringComparison.OrdinalIgnoreCase))
				&& (option.FileName is null || option.FileName.Equals(Path.GetFileName(fileName), StringComparison.OrdinalIgnoreCase));
		}

		private static String Show(ConfigOption? option, String? raw, String missing)
		{
			if (raw is null)
			{
				return Localizer.T(missing);
			}

			String shown = option?.DescribeRaw(raw) ?? raw;
			return shown == raw || raw.Length == 0 ? shown : shown + " (" + raw + ")";
		}
	}
}