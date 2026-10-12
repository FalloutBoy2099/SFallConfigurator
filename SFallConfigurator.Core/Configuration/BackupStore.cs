namespace SFallConfigurator.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;

	/// <summary>
	/// The files of one backup: the copies that were made when the settings were saved.
	/// </summary>
	internal sealed record BackupSet(String Name, DateTime Time, String Folder, IReadOnlyList<String> Files);

	/// <summary>
	/// Keeps the previous version of the game files each time the settings are saved, in the folder old_ini of the game:
	/// one sub-folder per save, named after the moment, with the files in the same places as in the game folder.
	/// </summary>
	internal sealed class BackupStore
	{
		/// <summary>
		/// The name of the backup folder in the game folder.
		/// </summary>
		public const String FolderName = "old_ini";

		/// <summary>
		/// How many saves are kept: the oldest ones are deleted.
		/// </summary>
		public const Int32 KeptSets = 20;

		private const String NameFormat = "yyyy-MM-dd_HH-mm-ss";

		private readonly String _gamePath;
		private readonly Func<DateTime> _clock;

		public BackupStore(String gamePath, Func<DateTime>? clock = null)
		{
			_gamePath = gamePath;
			_clock = clock ?? (() => DateTime.Now);
		}

		public String Folder => Path.Combine(_gamePath, FolderName);

		/// <summary>
		/// Copies the files that exist (and that are in the game folder) to a new backup. Returns null when there was nothing to copy.
		/// </summary>
		public BackupSet? Backup(IEnumerable<String> files)
		{
			List<(String Source, String Relative)> existing = [];
			foreach (String file in files.Distinct(StringComparer.OrdinalIgnoreCase))
			{
				String relative = Path.GetRelativePath(_gamePath, file);
				if (File.Exists(file) && !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative) && !relative.StartsWith(FolderName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
				{
					existing.Add((file, relative));
				}
			}

			if (existing.Count == 0)
			{
				return null;
			}

			DateTime now = _clock();
			String name = now.ToString(NameFormat, CultureInfo.InvariantCulture);
			String folder = Path.Combine(Folder, name);
			for (Int32 suffix = 2; Directory.Exists(folder); suffix++)
			{
				folder = Path.Combine(Folder, name + "_" + suffix.ToString(CultureInfo.InvariantCulture));
			}

			foreach ((String source, String relative) in existing)
			{
				String target = Path.Combine(folder, relative);
				Directory.CreateDirectory(Path.GetDirectoryName(target)!);
				File.Copy(source, target);
			}

			Prune();
			return new BackupSet(Path.GetFileName(folder), now, folder, existing.Select(file => file.Relative).ToList());
		}

		/// <summary>
		/// The backups, the newest first.
		/// </summary>
		public IReadOnlyList<BackupSet> GetSets()
		{
			if (!Directory.Exists(Folder))
			{
				return [];
			}

			List<BackupSet> sets = [];
			foreach (String folder in Directory.GetDirectories(Folder))
			{
				String name = Path.GetFileName(folder);
				DateTime time = DateTime.TryParseExact(name.Length >= NameFormat.Length ? name[..NameFormat.Length] : name, NameFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed)
					? parsed
					: Directory.GetCreationTime(folder);
				List<String> files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Select(file => Path.GetRelativePath(folder, file)).Order(StringComparer.OrdinalIgnoreCase).ToList();
				if (files.Count > 0)
				{
					sets.Add(new BackupSet(name, time, folder, files));
				}
			}

			return sets.OrderByDescending(set => set.Time).ThenByDescending(set => set.Name, StringComparer.Ordinal).ToList();
		}

		/// <summary>
		/// Puts the files of the backup back in the game folder. The current files should be backed up first (see <see cref="Backup"/>).
		/// </summary>
		public void Restore(BackupSet set)
		{
			foreach (String relative in set.Files)
			{
				String target = Path.Combine(_gamePath, relative);
				Directory.CreateDirectory(Path.GetDirectoryName(target)!);
				File.Copy(Path.Combine(set.Folder, relative), target, overwrite: true);
			}
		}

		private void Prune()
		{
			foreach (BackupSet old in GetSets().Skip(KeptSets))
			{
				try
				{
					Directory.Delete(old.Folder, recursive: true);
				}
				catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
				{
					// An old backup that stays is harmless.
				}
			}
		}
	}
}