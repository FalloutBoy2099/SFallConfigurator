namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Configuration;
	using Options;

	/// <summary>
	/// The "Graphics" tab of the sfall settings.
	/// </summary>
	internal sealed class GraphicsPage : OptionPage
	{
		private readonly TextOption _globalShaderFile;
		private readonly ChoiceOption _mode;

		public GraphicsPage(OptionContext context, HiResResolutionEditor? hiResEditor)
			: base("Graphics", context)
		{
			GameConfiguration configuration = context.Configuration;
			OptionLocation sfall = context.Sfall;

			_mode = Add(new ChoiceOption(
				sfall.In("Graphics"),
				"Mode",
				"Graphics Mode:",
				[
					"0 - Off: Using settings of HRP by Mash",
					"1 - DX7: Fullscreen",
					"2 - DX7: Windowed",
					"3 - DX7: Full Desktop Windowed",
					"4 - DX9: Fullscreen",
					"5 - DX9: Windowed",
					"6 - DX9: Full Desktop Windowed",
				])
			{
				ToolTip = "The graphics mode set here overrides the mode set in f2_res.ini.\nNote:\nThe DX7 modes are used by the Hi-Res patch built into sFall since v5.0.\nFor the built-in Hi-Res patch, mode 0 (Off) works like DX7 Fullscreen.",
				IndexFromRaw = raw => IniValue.ToInt32(raw) is >= 1 and <= 6 ? IniValue.ToInt32(raw) : 0,
				Written = raw =>
				{
					// The mode is kept in both files, as the original tool did.
					configuration.Ddraw.SetValue("Graphics", "Mode", raw, IniWriteMode.AddIfMissing);
					configuration.Sfall?.SetValue("Graphics", "Mode", raw, IniWriteMode.AddIfMissing);
				},
			});

			NumberOption width = Add(new NumberOption(sfall, "GraphicsWidth", "Width", 0, 10000) { Increment = 10 });
			NumberOption height = Add(new NumberOption(sfall, "GraphicsHeight", "Height", 0, 10000) { Increment = 10 });
			Resolution = new SfallResolutionEditor(width, height, hiResEditor);
			_mode.PropertyChanged += (_, _) => Resolution.IsScaleAvailable = _mode.SelectedIndex > 0;

			BoolOption textureFilter = Add(new BoolOption(sfall, "TextureFilter", "Force Anti-aliasing Filter")
			{
				TrueValue = "2",
				Interpret = raw => raw == "2",
			});

			ChoiceOption movieFilter = Add(new ChoiceOption(
				sfall,
				"MovieFilter",
				"Movie Filter:",
				["Bilinear (Low Quality)", "Catmull-Rom", "Lanczos2 (High Quality)"])
			{
				ToolTip = "Sets the scaling quality of the game movies.",
				Values = ["1", "2", "3"],
			});

			BoolOption limitFps = Bool(
				sfall,
				"LimitFPS",
				"FPS Limit",
				"Limits a high FPS in the game (DX9 mode).\nIf your graphics card gets very hot while playing, enable this option to reduce its load.\nOtherwise it is not recommended, as it may affect the rendering of the game picture.");

			_globalShaderFile = Add(new TextOption(sfall, "GlobalShaderFile", "Global Shader File")
			{
				ToolTip = "Uses a hardware shader in the DX9 graphics mode.",
				Suggestions = FindShaders(configuration),
			});

			Groups =
			[
				new OptionGroup(null, _mode),
				new OptionGroup("Screen Resolution", Resolution),
				new OptionGroup("Quality", textureFilter, movieFilter, limitFps, _globalShaderFile),
				new OptionGroup(
					null,
					new OptionNote("Note: If the game runs with a bad FPS (slowdown) in DX9 Fullscreen mode, switch to DirectX 9 in the \"Hi-Res Config\" tab, after setting the \"Graphics Mode\" option here to Off. If that does not help, disable VSync for your graphics card.")),
			];
		}

		public SfallResolutionEditor Resolution { get; }

		public override void Load()
		{
			base.Load();
			Resolution.IsScaleAvailable = _mode.SelectedIndex > 0;
			Resolution.Refresh();
			_globalShaderFile.Suggestions = FindShaders(Context.Configuration);
		}

		private static IReadOnlyList<String> FindShaders(GameConfiguration configuration)
		{
			String directory = configuration.GetShadersDirectory();
			if (!Directory.Exists(directory))
			{
				return [];
			}

			return Directory.GetFiles(directory, "*.fx", SearchOption.TopDirectoryOnly)
				.Select(path => Path.GetFileName(path))
				.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
				.ToList();
		}
	}
}