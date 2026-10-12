namespace SFallConfigurator.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	/// <summary>
	/// Copies option values from an older ini file into a newer one.
	/// </summary>
	internal static class ConfigMerger
	{
		/// <summary>
		/// Applies every non-empty value of <paramref name="sourcePath"/> to the matching key of
		/// <paramref name="target"/>. Keys that do not exist in the target are not added.
		/// </summary>
		public static void Merge(IniDocument target, String sourcePath, Boolean carryModifiedIni)
		{
			if (!File.Exists(sourcePath))
			{
				return;
			}

			IniDocument source = IniDocument.LoadDetected(sourcePath);

			String? section = null;
			foreach (String line in source.Lines)
			{
				if (IniDocument.TryParseSection(line, out String name))
				{
					section = name;
					continue;
				}

				if (section is not null
					&& IniDocument.TryParseEntry(line, out String key, out String value)
					&& value.Length > 0)
				{
					target.SetValue(section, key, value, IniWriteMode.UpdateExisting);
				}
			}

			if (carryModifiedIni)
			{
				CarryModifiedIni(target, source);
			}
		}

		private static void CarryModifiedIni(IniDocument target, IniDocument source)
		{
			String? modifiedIni = source.GetValue("Main", "ModifiedIni");
			Int32 mainIndex = target.FindSection("Main");
			if (modifiedIni is null || mainIndex < 0 || target.GetValue("Main", "ModifiedIni") is not null)
			{
				return;
			}

			target.InsertLines(mainIndex + 1, ["ModifiedIni=" + modifiedIni, String.Empty]);
		}
	}

	/// <summary>
	/// The "-install" start mode used by installers: the settings saved in the .bak files of the
	/// previous version are merged into the freshly installed ini files, then the program ends.
	/// </summary>
	internal static class InstallMode
	{
		private const String Switch = "-install";

		/// <summary>
		/// Runs the install mode when requested. Returns null for a normal start, otherwise whether
		/// any settings were merged.
		/// </summary>
		public static Boolean? TryRun(String gamePath, IEnumerable<String> arguments)
		{
			// The switch is an argument of its own: "-install" inside the path of the game folder is not the switch.
			List<String> list = arguments.ToList();
			Int32 switchIndex = list.FindIndex(IsSwitch);
			if (switchIndex < 0)
			{
				return null;
			}

			// The character after the switch is a flag ('+' deletes the backups), the rest is the sfall-mods.ini path.
			String joined = String.Join(" ", list.Skip(switchIndex));
			String rest = joined.Substring(Switch.Length).TrimStart(' ');
			Boolean deleteBackups = rest.StartsWith('+');
			String modsIniPath = rest.Length > 1 ? rest.Substring(1).Trim().Trim('"') : String.Empty;

			Boolean merged = MergeDdraw(gamePath, deleteBackups);
			merged |= MergeSfallMods(gamePath, modsIniPath, deleteBackups);
			return merged;
		}

		/// <summary>
		/// "-install", alone or followed by a space or by the flag ("-install+", "-install-").
		/// </summary>
		private static Boolean IsSwitch(String argument)
		{
			return argument.StartsWith(Switch, StringComparison.OrdinalIgnoreCase)
				&& (argument.Length == Switch.Length || " +-".Contains(argument[Switch.Length]));
		}

		private static Boolean MergeDdraw(String gamePath, Boolean deleteBackup)
		{
			String backupPath = Path.Combine(gamePath, "ddraw.ini.bak");
			String ddrawPath = Path.Combine(gamePath, "ddraw.ini");
			if (!File.Exists(backupPath) || !File.Exists(ddrawPath))
			{
				return false;
			}

			IniDocument ddraw = IniDocument.LoadDetected(ddrawPath);
			ConfigMerger.Merge(ddraw, backupPath, true);
			ddraw.Save();

			if (deleteBackup)
			{
				File.Delete(backupPath);
			}

			return true;
		}

		private static Boolean MergeSfallMods(String gamePath, String modsIniPath, Boolean deleteBackup)
		{
			String backupPath = Path.Combine(gamePath, "sfall-mods.ini.bak");
			if (!File.Exists(backupPath))
			{
				return false;
			}

			String modsPath = String.IsNullOrEmpty(modsIniPath) ? String.Empty : Path.Combine(gamePath, modsIniPath);
			if (!File.Exists(modsPath))
			{
				modsPath = Path.Combine(gamePath, "sfall-mods.ini");
			}

			if (!File.Exists(modsPath))
			{
				return false;
			}

			IniDocument mods = IniDocument.LoadDetected(modsPath);
			ConfigMerger.Merge(mods, backupPath, false);
			mods.Save();

			if (deleteBackup)
			{
				File.Delete(backupPath);
			}

			return true;
		}
	}
}