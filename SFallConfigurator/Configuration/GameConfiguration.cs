namespace SFallConfigurator.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	/// <summary>
	/// All configuration files of one game installation: ddraw.ini, the optional custom sfall ini,
	/// f2_res.ini, sfall-mods.ini, the game's own fallout2.cfg, the translations and the
	/// configurator's own settings.
	/// </summary>
	internal sealed class GameConfiguration
	{
		private const String DdrawFileName = "ddraw.ini";
		private const String DefaultSfallFileName = "sfall.ini";
		private const String HiResFileName = "f2_res.ini";
		private const String DefaultSfallModsFileName = "sfall-mods.ini";
		private const String DefaultGameCfgFileName = "fallout2.cfg";
		private const String DefaultTranslationsFileName = "translations.ini";
		private const String DefaultGameExe = "FALLOUT2.EXE";

		private const String MainSection = "Main";
		private const String SettingGameExe = "game_exe";
		private const String SettingCustomIni = "custom_ini";
		private const String SettingGameCfg = "game_cfg";
		private const String SettingLanguages = "lng_list";

		private static readonly String[] DebugSection =
		[
			"[debug]",
			"output_map_data_info=1",
			"show_load_info=1",
			"show_script_messages=1",
			"show_tile_num=0",
			String.Empty,
		];

		private readonly String _settingsPath;
		private readonly String _legacySettingsPath;
		private readonly List<String> _languages = new();

		public GameConfiguration(String gamePath)
		{
			GamePath = gamePath;
			_settingsPath = Path.Combine(gamePath, "sfall", "setting.sfc");
			_legacySettingsPath = Path.Combine(gamePath, "setting.sfc");

			Ddraw = IniDocument.CreateEmpty(Path.Combine(gamePath, DdrawFileName), TextEncodings.Utf8WithBom);
			Settings = CreateDefaultSettings();
			GameCfg = IniDocument.CreateEmpty(PathHelper.Resolve(gamePath, DefaultGameCfgFileName), TextEncodings.Ansi);
			Translations = IniDocument.CreateEmpty(PathHelper.Resolve(gamePath, DefaultTranslationsFileName), TextEncodings.Ansi);

			SfallSource = new SfallSource(this);
			SfallModsSource = new DocumentSource(() => SfallMods, IniWriteMode.UpdateExisting);
			HiResSource = new DocumentSource(() => HiRes, IniWriteMode.AddIfMissing);
			GameCfgSource = new DocumentSource(() => GameCfg, IniWriteMode.AddIfMissing);
			TranslationsSource = new DocumentSource(() => Translations, IniWriteMode.AddIfMissing);
		}

		public String GamePath { get; }

		/// <summary>
		/// ddraw.ini, which carries the sfall settings unless a custom sfall ini is used.
		/// </summary>
		public IniDocument Ddraw { get; private set; }

		/// <summary>
		/// The custom sfall configuration file (sfall.ini by default), when there is one.
		/// </summary>
		public IniDocument? Sfall { get; private set; }

		public IniDocument? HiRes { get; private set; }

		public IniDocument? SfallMods { get; private set; }

		public IniDocument GameCfg { get; private set; }

		public IniDocument Translations { get; private set; }

		/// <summary>
		/// The configurator's own settings (setting.sfc).
		/// </summary>
		public IniDocument Settings { get; private set; }

		public IConfigSource SfallSource { get; }

		public IConfigSource SfallModsSource { get; }

		public IConfigSource HiResSource { get; }

		public IConfigSource GameCfgSource { get; }

		public IConfigSource TranslationsSource { get; }

		public Boolean DdrawExists => File.Exists(Ddraw.Path);

		/// <summary>
		/// Game relative name of the file that holds the sfall options, as shown to the user.
		/// </summary>
		public String? SfallFileName => Sfall is null ? null : Path.GetRelativePath(GamePath, Sfall.Path);

		/// <summary>
		/// Game relative path of sfall-mods.ini, used to open it in an editor.
		/// </summary>
		public String SfallModsFileName => SfallMods is null ? DefaultSfallModsFileName : Path.GetRelativePath(GamePath, SfallMods.Path);

		public String GameCfgFileName => Path.GetRelativePath(GamePath, GameCfg.Path);

		public IReadOnlyList<String> Languages => _languages;

		public String GameExe
		{
			get
			{
				String? exe = Settings.GetValue(MainSection, SettingGameExe);
				return String.IsNullOrEmpty(exe) ? DefaultGameExe : exe;
			}
			set => Settings.SetValue(MainSection, SettingGameExe, value, IniWriteMode.AddIfMissing);
		}

		public String GameExePath => Path.Combine(GamePath, GameExe);

		public Boolean HasPendingChanges =>
			Ddraw.IsModified || Settings.IsModified || GameCfg.IsModified || Translations.IsModified
			|| (Sfall?.IsModified ?? false) || (HiRes?.IsModified ?? false) || (SfallMods?.IsModified ?? false);

		/// <summary>
		/// Reads every configuration file that exists.
		/// </summary>
		public void Load()
		{
			LoadSettings();

			if (File.Exists(Ddraw.Path))
			{
				Ddraw = IniDocument.Load(Ddraw.Path, TextEncodings.DetectFileEncoding(Ddraw.Path), TextEncodings.Utf8WithBom);
			}

			LoadSfallConfig();
			LoadHiRes();
			LoadSfallMods();
			LoadGameCfg();
			LoadTranslations();
		}

		/// <summary>
		/// Writes every document that was changed, with the files staged by the caller (the load order of the mods).
		/// Every file is written completely before one of them is replaced, and the replacement is all or none:
		/// when a file cannot be written or replaced, none of the game files keeps a change.
		/// </summary>
		public void Save(IEnumerable<StagedFile>? additionalFiles = null)
		{
			List<StagedFile> staged = [.. additionalFiles ?? []];
			try
			{
				foreach (IniDocument? document in new[] { Ddraw, Sfall, HiRes, SfallMods, GameCfg, Translations, Settings })
				{
					if (document is { IsModified: true })
					{
						staged.Add(document.Stage());
					}
				}
			}
			catch
			{
				staged.ForEach(file => file.Discard());
				throw;
			}

			StagedFile.CommitAll(staged);
		}

		/// <summary>
		/// Switches to another sfall configuration file and remembers the choice.
		/// </summary>
		public void SetCustomSfallFile(String path)
		{
			Sfall = LoadCustomSfall(path);
			Settings.SetValue(MainSection, SettingCustomIni, ToStoredPath(path), IniWriteMode.AddIfMissing);
		}

		/// <summary>
		/// Creates sfall.ini as a copy of the current ddraw.ini settings.
		/// </summary>
		public void CreateSfallIni()
		{
			Sfall = new IniDocument(PathHelper.Resolve(GamePath, DefaultSfallFileName), Ddraw.Lines, TextEncodings.Utf8WithBom);
			Sfall.MarkModified();
		}

		public void SetGameCfgFile(String path)
		{
			if (String.Equals(Path.GetFullPath(path), Path.GetFullPath(GameCfg.Path), StringComparison.OrdinalIgnoreCase))
			{
				return;
			}

			Settings.SetValue(MainSection, SettingGameCfg, ToStoredPath(path), IniWriteMode.AddIfMissing);
			LoadGameCfg(path);
			LoadTranslations();
		}

		public void AddLanguage(String language)
		{
			if (String.IsNullOrWhiteSpace(language) || _languages.Contains(language, StringComparer.OrdinalIgnoreCase))
			{
				return;
			}

			_languages.Add(language);
			Settings.SetValue(MainSection, SettingLanguages, String.Join(",", _languages), IniWriteMode.AddIfMissing);
		}

		/// <summary>
		/// Adds the [debug] section to fallout2.cfg when it is missing, so the debug mode has an effect.
		/// </summary>
		public void EnsureDebugSection()
		{
			if (GameCfg.LineCount > 0 && !GameCfg.HasSection("debug"))
			{
				GameCfg.InsertLines(0, DebugSection);
			}
		}

		/// <summary>
		/// Folder that holds the shader files (.fx) of the game.
		/// </summary>
		public String GetShadersDirectory()
		{
			String? masterPatches = GameCfg.GetValue("system", "master_patches");
			return String.IsNullOrWhiteSpace(masterPatches)
				? Path.Combine(GamePath, "data", "shaders")
				: Path.Combine(PathHelper.Resolve(GamePath, masterPatches), "shaders");
		}

		private IniDocument CreateDefaultSettings()
		{
			return new IniDocument(
				_settingsPath,
				[
					"[" + MainSection + "]",
					SettingGameExe + "=",
					SettingCustomIni + "=",
					SettingGameCfg + "=",
					SettingLanguages + "=",
				],
				TextEncodings.Ansi);
		}

		private void LoadSettings()
		{
			String? existing = File.Exists(_settingsPath) ? _settingsPath : File.Exists(_legacySettingsPath) ? _legacySettingsPath : null;

			// The legacy location is read once; the settings are always saved to the new one.
			Settings = existing is null
				? CreateDefaultSettings()
				: new IniDocument(_settingsPath, File.ReadAllLines(existing, TextEncodings.Ansi), TextEncodings.Ansi);

			_languages.Clear();
			String? languages = Settings.GetValue(MainSection, SettingLanguages);
			if (!String.IsNullOrEmpty(languages))
			{
				_languages.AddRange(languages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
			}
		}

		private void LoadSfallConfig()
		{
			Sfall = null;

			String? custom = Settings.GetValue(MainSection, SettingCustomIni);
			String? path = null;
			if (!String.IsNullOrEmpty(custom) && File.Exists(PathHelper.Resolve(GamePath, custom)))
			{
				path = PathHelper.Resolve(GamePath, custom);
			}
			else if (File.Exists(PathHelper.Resolve(GamePath, DefaultSfallFileName)))
			{
				path = PathHelper.Resolve(GamePath, DefaultSfallFileName);
			}

			if (path is not null)
			{
				Sfall = LoadCustomSfall(path);
			}
		}

		/// <summary>
		/// ddraw.ini can be chosen as the sfall file: it is then the only document of that file.
		/// </summary>
		private IniDocument? LoadCustomSfall(String path)
		{
			return String.Equals(Path.GetFullPath(path), Path.GetFullPath(Ddraw.Path), StringComparison.OrdinalIgnoreCase)
				? null
				: IniDocument.Load(path, TextEncodings.DetectFileEncoding(path), TextEncodings.Utf8WithBom);
		}

		private void LoadHiRes()
		{
			String path = PathHelper.Resolve(GamePath, HiResFileName);
			HiRes = File.Exists(path) ? IniDocument.Load(path, TextEncodings.Ansi, TextEncodings.Ansi) : null;
		}

		private void LoadSfallMods()
		{
			SfallMods = null;

			String? folder = SfallSource.Read(null, "IniConfigFolder");
			if (folder is not null)
			{
				String relative = folder.Replace('/', '\\').Trim().Trim('\\');
				String candidate = PathHelper.Resolve(GamePath, Path.Combine(relative, DefaultSfallModsFileName));
				if (File.Exists(candidate))
				{
					SfallMods = IniDocument.Load(candidate, TextEncodings.DetectFileEncoding(candidate), TextEncodings.Utf8WithBom);
					return;
				}
			}

			String fallback = PathHelper.Resolve(GamePath, DefaultSfallModsFileName);
			if (File.Exists(fallback))
			{
				SfallMods = IniDocument.Load(fallback, TextEncodings.DetectFileEncoding(fallback), TextEncodings.Utf8WithBom);
			}
		}

		private void LoadGameCfg()
		{
			String? configured = SfallSource.Read(null, "ConfigFile");
			String? stored = Settings.GetValue(MainSection, SettingGameCfg);

			String name = !String.IsNullOrEmpty(configured) ? configured
				: !String.IsNullOrEmpty(stored) ? stored
				: DefaultGameCfgFileName;

			LoadGameCfg(PathHelper.Resolve(GamePath, name));
		}

		private void LoadGameCfg(String path)
		{
			GameCfg = File.Exists(path)
				? IniDocument.Load(path, TextEncodings.Ansi, TextEncodings.Ansi)
				: IniDocument.CreateEmpty(path, TextEncodings.Ansi);
		}

		private void LoadTranslations()
		{
			String? configured = SfallSource.Read(null, "TranslationsINI");
			String fileName = configured is null ? DefaultTranslationsFileName : configured.TrimStart('.', '\\', '/');

			String? masterPatches = GameCfg.GetValue("system", "master_patches");
			if (masterPatches is not null)
			{
				String? language = GameCfg.GetValue("system", "language");
				if (String.IsNullOrEmpty(language))
				{
					language = "english";
				}

				String localized = PathHelper.Resolve(GamePath, Path.Combine(masterPatches, "text", language, fileName));
				if (File.Exists(localized))
				{
					Translations = IniDocument.Load(localized, TextEncodings.Ansi, TextEncodings.Ansi);
					return;
				}
			}

			String direct = PathHelper.Resolve(GamePath, fileName);
			if (File.Exists(direct))
			{
				Translations = IniDocument.Load(direct, TextEncodings.Ansi, TextEncodings.Ansi);
				return;
			}

			Translations = IniDocument.CreateEmpty(PathHelper.Resolve(GamePath, DefaultTranslationsFileName), TextEncodings.Ansi);
		}

		/// <summary>
		/// Settings store game relative paths with a leading backslash, as the earlier versions did.
		/// </summary>
		private String ToStoredPath(String path)
		{
			String fullPath = Path.GetFullPath(path);
			String relative = Path.GetRelativePath(GamePath, fullPath);
			return Path.IsPathFullyQualified(relative) || relative.StartsWith("..", StringComparison.Ordinal)
				? fullPath
				: "\\" + relative;
		}
	}
}