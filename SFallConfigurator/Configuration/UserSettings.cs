namespace SFallConfigurator.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;

	/// <summary>
	/// The settings of the user that belong to the configurator itself (not to a game): the recent game folders,
	/// the language of the interface and the placement of the window. They are stored as "key=value" lines in
	/// %LOCALAPPDATA%\SFallConfigurator\settings.txt; a key may appear several times.
	/// </summary>
	internal static class UserSettings
	{
		private const String SettingsFileName = "settings.txt";
		private const String FolderName = "SFallConfigurator";
		private const String LegacyFolderName = "SFallConfiguratorMk2";

		/// <summary>
		/// Folder that holds the settings file. Only the tests change it.
		/// </summary>
		internal static String? DirectoryOverride { get; set; }

		public static String? Get(String key)
		{
			return GetAll(key).FirstOrDefault();
		}

		public static IReadOnlyList<String> GetAll(String key)
		{
			return Read().Where(entry => entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Select(entry => entry.Value).ToList();
		}

		/// <summary>
		/// Stores one value for the key, or removes the key when the value is null.
		/// </summary>
		public static void Set(String key, String? value)
		{
			SetAll(key, value is null ? [] : [value]);
		}

		/// <summary>
		/// Replaces every value of the key. The other keys are kept as they are.
		/// </summary>
		public static void SetAll(String key, IEnumerable<String> values)
		{
			List<(String Key, String Value)> entries = Read();
			Int32 position = entries.FindIndex(entry => entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
			entries.RemoveAll(entry => entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
			entries.InsertRange(position < 0 ? entries.Count : position, values.Select(value => (key, value)));
			Write(entries);
		}

		private static List<(String Key, String Value)> Read()
		{
			List<(String Key, String Value)> entries = [];
			try
			{
				String file = GetPath();
				if (File.Exists(file))
				{
					foreach (String line in File.ReadAllLines(file))
					{
						if (IniDocument.TryParseEntry(line, out String key, out String value) && value.Length > 0)
						{
							entries.Add((key, value));
						}
					}
				}
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				// The settings are only a convenience: a missing memory is not an error.
			}

			return entries;
		}

		private static void Write(List<(String Key, String Value)> entries)
		{
			try
			{
				String file = GetPath();
				Directory.CreateDirectory(Path.GetDirectoryName(file)!);
				StagedFile.WriteAllLines(file, entries.Select(entry => entry.Key + "=" + entry.Value), new UTF8Encoding(false));
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				// Same as above: failing to write the memory is not an error.
			}
		}

		private static String GetPath()
		{
			if (DirectoryOverride is not null)
			{
				return Path.Combine(DirectoryOverride, SettingsFileName);
			}

			String localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			String file = Path.Combine(localData, FolderName, SettingsFileName);
			MigrateLegacySettings(file, Path.Combine(localData, LegacyFolderName, SettingsFileName));
			return file;
		}

		/// <summary>
		/// The program was called SFallConfiguratorMk2 before: its settings are copied once to the new folder,
		/// so that the recent game folders, the language and the window placement are not lost by the rename.
		/// </summary>
		internal static void MigrateLegacySettings(String file, String legacyFile)
		{
			try
			{
				if (!File.Exists(file) && File.Exists(legacyFile))
				{
					Directory.CreateDirectory(Path.GetDirectoryName(file)!);
					File.Copy(legacyFile, file);
				}
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				// The settings are only a convenience: starting without them is not an error.
			}
		}
	}
}