namespace SFallConfigurator.Configuration
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// What happened to a setting of a file.
	/// </summary>
	internal enum ChangeKind
	{
		Changed,
		Added,
		Removed,
	}

	/// <summary>
	/// One setting of a configuration file that differs from the saved file.
	/// </summary>
	internal sealed record IniChange(ChangeKind Kind, String? Section, String Key, String? Before, String? After);

	/// <summary>
	/// The settings of one file that differ from the saved file.
	/// </summary>
	internal sealed record FileChanges(String FileName, IReadOnlyList<IniChange> Changes);

	/// <summary>
	/// Finds the settings that differ between two versions of an ini file. The comments and the layout are not compared.
	/// </summary>
	internal static class IniComparer
	{
		public static IReadOnlyList<IniChange> Compare(IReadOnlyList<String> before, IReadOnlyList<String> after)
		{
			List<(String Section, String Key, String Value)> oldEntries = Parse(before);
			List<(String Section, String Key, String Value)> newEntries = Parse(after);

			Dictionary<(String, String), String> oldValues = [];
			foreach ((String section, String key, String value) in oldEntries)
			{
				oldValues.TryAdd((section.ToLowerInvariant(), key.ToLowerInvariant()), value);
			}

			Dictionary<(String, String), String> newValues = [];
			foreach ((String section, String key, String value) in newEntries)
			{
				newValues.TryAdd((section.ToLowerInvariant(), key.ToLowerInvariant()), value);
			}

			List<IniChange> changes = [];
			HashSet<(String, String)> seen = [];
			foreach ((String section, String key, String value) in newEntries)
			{
				(String, String) id = (section.ToLowerInvariant(), key.ToLowerInvariant());
				if (!seen.Add(id))
				{
					continue;
				}

				if (!oldValues.TryGetValue(id, out String? previous))
				{
					changes.Add(new IniChange(ChangeKind.Added, section.Length == 0 ? null : section, key, null, newValues[id]));
				}
				else if (previous != newValues[id])
				{
					changes.Add(new IniChange(ChangeKind.Changed, section.Length == 0 ? null : section, key, previous, newValues[id]));
				}
			}

			foreach ((String section, String key, String value) in oldEntries)
			{
				(String, String) id = (section.ToLowerInvariant(), key.ToLowerInvariant());
				if (!newValues.ContainsKey(id) && seen.Add(id))
				{
					changes.Add(new IniChange(ChangeKind.Removed, section.Length == 0 ? null : section, key, value, null));
				}
			}

			return changes;
		}

		private static List<(String Section, String Key, String Value)> Parse(IReadOnlyList<String> lines)
		{
			List<(String, String, String)> entries = [];
			String section = String.Empty;
			foreach (String line in lines)
			{
				if (IniDocument.TryParseSection(line, out String name))
				{
					section = name;
				}
				else if (IniDocument.TryParseEntry(line, out String key, out String value))
				{
					entries.Add((section, key, value));
				}
			}

			return entries;
		}
	}
}