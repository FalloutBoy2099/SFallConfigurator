namespace SFallConfigurator.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	/// <summary>
	/// Decides which game folder the configurator works on, and remembers the folders that were used.
	/// </summary>
	internal static class GameLocator
	{
		/// <summary>
		/// How many game folders are remembered.
		/// </summary>
		public const Int32 MaximumRecentFolders = 10;

		private const String RecentFolderKey = "game_path";
		private const String LegacyLastFolderKey = "last_game_path";

		/// <summary>
		/// Command line switch that names the game folder: "--game C:\Games\Fallout2" or "--game=C:\Games\Fallout2".
		/// </summary>
		private const String GameSwitch = "--game";

		/// <summary>
		/// Picks the folder to open at start: the one named on the command line, otherwise the folder used last
		/// time, otherwise the folder of the program.
		/// </summary>
		public static String Resolve(IReadOnlyList<String> arguments)
		{
			String? fromArguments = ReadSwitch(arguments);
			if (fromArguments is not null && Directory.Exists(fromArguments))
			{
				return Normalize(fromArguments);
			}

			return GetRecentFolders().FirstOrDefault() ?? PathHelper.GetApplicationDirectory();
		}

		/// <summary>
		/// The full path of a folder, without the separator at the end ("D:\Games\F2\" becomes "D:\Games\F2"),
		/// except for the root of a drive, where it is part of the path ("D:\").
		/// </summary>
		public static String Normalize(String path)
		{
			String full = Path.GetFullPath(path);
			return String.Equals(full, Path.GetPathRoot(full), StringComparison.Ordinal) ? full : full.TrimEnd('\\', '/');
		}

		public static Boolean LooksLikeGameFolder(String path)
		{
			return File.Exists(Path.Combine(path, "ddraw.ini")) || File.Exists(Path.Combine(path, "fallout2.exe"));
		}

		/// <summary>
		/// The game folders used before, most recent first. Folders that are gone or are no longer game folders are left out.
		/// </summary>
		public static IReadOnlyList<String> GetRecentFolders()
		{
			return ReadStoredFolders().Where(path => Directory.Exists(path) && LooksLikeGameFolder(path)).ToList();
		}

		/// <summary>
		/// Puts a folder at the top of the remembered ones. Only a folder that holds a game is remembered.
		/// </summary>
		public static void RememberGamePath(String path)
		{
			if (!LooksLikeGameFolder(path))
			{
				return;
			}

			String full = Normalize(path);
			List<String> folders = ReadStoredFolders().Where(stored => !SamePath(stored, full)).ToList();
			folders.Insert(0, full);

			UserSettings.SetAll(RecentFolderKey, folders.Take(MaximumRecentFolders));
			UserSettings.Set(LegacyLastFolderKey, null);
		}

		private static Boolean SamePath(String left, String right)
		{
			return String.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
		}

		private static List<String> ReadStoredFolders()
		{
			List<String> folders = [];
			foreach (String value in UserSettings.GetAll(RecentFolderKey).Concat(UserSettings.GetAll(LegacyLastFolderKey)))
			{
				if (!folders.Any(known => SamePath(known, value)))
				{
					folders.Add(value);
				}
			}

			return folders;
		}

		private static String? ReadSwitch(IReadOnlyList<String> arguments)
		{
			for (Int32 index = 0; index < arguments.Count; index++)
			{
				String argument = arguments[index];
				if (argument.StartsWith(GameSwitch + "=", StringComparison.OrdinalIgnoreCase))
				{
					return argument.Substring(GameSwitch.Length + 1).Trim('"');
				}

				if (argument.Equals(GameSwitch, StringComparison.OrdinalIgnoreCase) && index + 1 < arguments.Count)
				{
					return arguments[index + 1].Trim('"');
				}
			}

			return null;
		}
	}
}