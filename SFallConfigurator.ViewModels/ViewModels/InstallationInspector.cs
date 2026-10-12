namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using Configuration;
	using Infrastructure;
	using Localization;
	using Services;

	/// <summary>
	/// Looks at the game folder and says what is installed (the game, sfall, the Hi-Res patch, the mods), and what is likely to be a
	/// problem. Nothing is changed.
	/// </summary>
	internal static class InstallationInspector
	{
		private const String SfallDllName = "ddraw.dll";
		private const String RenamedSfallDllName = "sfall.dll";
		private const String MashHiResDllName = "f2_res.dll";

		public static IReadOnlyList<ReportLine> Inspect(GameConfiguration configuration, AppServices services, HiResMode activeHiResMode)
		{
			List<ReportLine> lines = [];
			String gamePath = configuration.GamePath;

			// The game
			String exe = configuration.GameExe;
			if (File.Exists(configuration.GameExePath))
			{
				lines.Add(new ReportLine(ReportSeverity.Ok, Localizer.F("The game executable {0} was found.", exe)));
			}
			else
			{
				lines.Add(new ReportLine(ReportSeverity.Error, Localizer.F("The game executable {0} was not found.", exe)));
			}

			if (IsProtectedFolder(gamePath))
			{
				lines.Add(new ReportLine(ReportSeverity.Warning, Localizer.F("The game is in a protected folder ({0}): saving may need the rights of an administrator.", gamePath)));
			}

			// sfall: ddraw.dll, or sfall.dll when the executable loads it under that name
			Boolean renamed = File.Exists(configuration.GameExePath) && services.ExecutablePatcher.GetSfallDllState(configuration.GameExePath) == DllPatchState.Patched;
			String dllName = renamed ? RenamedSfallDllName : SfallDllName;
			String dllPath = Path.Combine(gamePath, dllName);
			if (File.Exists(dllPath))
			{
				String? version = GetVersion(dllPath);
				lines.Add(new ReportLine(ReportSeverity.Ok, version is null
					? Localizer.F("sfall is installed ({0}).", dllName)
					: Localizer.F("sfall {0} is installed ({1}).", version, dllName)));
			}
			else
			{
				lines.Add(new ReportLine(ReportSeverity.Warning, Localizer.F("{0} was not found: sfall is not installed, and its settings have no effect.", dllName)));
			}

			// The Hi-Res patch
			Boolean sfallHiRes = IniValue.ToInt32(configuration.Ddraw.GetValue(null, "HiResMode") ?? "0") > 0;
			Boolean mashFile = File.Exists(Path.Combine(gamePath, MashHiResDllName));
			if (sfallHiRes && (mashFile || activeHiResMode == HiResMode.Mash))
			{
				lines.Add(new ReportLine(ReportSeverity.Warning, Localizer.F("Both Hi-Res patches are in use (the one of sfall and {0}): keep only one.", MashHiResDllName)));
			}
			else if (sfallHiRes)
			{
				lines.Add(new ReportLine(ReportSeverity.Ok, Localizer.T("The Hi-Res patch built into sfall is used.")));
			}
			else if (activeHiResMode == HiResMode.Mash)
			{
				lines.Add(new ReportLine(ReportSeverity.Ok, Localizer.F("The Hi-Res patch of Mash is used ({0}).", MashHiResDllName)));
			}
			else
			{
				lines.Add(new ReportLine(ReportSeverity.Info, Localizer.T("No Hi-Res patch is used.")));
			}

			// The data of the game
			foreach (String key in new[] { "master_dat", "critter_dat" })
			{
				String? value = configuration.GameCfg.GetValue("system", key);
				if (!String.IsNullOrWhiteSpace(value) && !File.Exists(PathHelper.Resolve(gamePath, value)) && !Directory.Exists(PathHelper.Resolve(gamePath, value)))
				{
					lines.Add(new ReportLine(ReportSeverity.Warning, Localizer.F("{0} was not found: {1}", key, value)));
				}
			}

			// The mods
			String modsFolder = Path.Combine(gamePath, "mods");
			if (Directory.Exists(modsFolder))
			{
				Int32 count = Directory.GetDirectories(modsFolder).Length + Directory.GetFiles(modsFolder, "*.dat").Length;
				lines.Add(new ReportLine(ReportSeverity.Info, Localizer.F("{0} mods are in the mods folder.", count)));
			}

			// The files that the program writes
			foreach (IniDocument? document in new[] { configuration.Ddraw, configuration.Sfall, configuration.HiRes, configuration.SfallMods, configuration.GameCfg, configuration.Translations })
			{
				if (document is not null && File.Exists(document.Path) && new FileInfo(document.Path).IsReadOnly)
				{
					lines.Add(new ReportLine(ReportSeverity.Error, Localizer.F("{0} is read-only: the settings cannot be saved in it.", Path.GetFileName(document.Path))));
				}
			}

			if (!lines.Any(line => line.Severity is ReportSeverity.Warning or ReportSeverity.Error))
			{
				lines.Add(new ReportLine(ReportSeverity.Ok, Localizer.T("Nothing wrong was found.")));
			}

			return lines;
		}

		/// <summary>
		/// The version that the file says it has, written with dots; null when it says none.
		/// </summary>
		public static String? GetVersion(String path)
		{
			try
			{
				String? version = FileVersionInfo.GetVersionInfo(path).FileVersion;
				if (String.IsNullOrWhiteSpace(version))
				{
					return null;
				}

				return version.Replace(", ", ".", StringComparison.Ordinal).Replace(",", ".", StringComparison.Ordinal).Trim();
			}
			catch (Exception exception) when (ErrorGuard.IsFileError(exception))
			{
				return null;
			}
		}

		private static Boolean IsProtectedFolder(String gamePath)
		{
			String full = Path.GetFullPath(gamePath);
			return new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.Windows }
				.Select(Environment.GetFolderPath)
				.Where(folder => folder.Length > 0)
				.Any(folder => full.StartsWith(folder, StringComparison.OrdinalIgnoreCase));
		}
	}
}