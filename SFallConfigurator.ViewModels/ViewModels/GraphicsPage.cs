namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Configuration;
	using Options;

	/// <summary>
	/// The "Graphics" tab of the sfall settings. The catalog says what the options are; the page says how they are read and written.
	/// </summary>
	internal sealed class GraphicsPage : OptionPage
	{
		private readonly TextOption _globalShaderFile;
		private readonly ChoiceOption _mode;

		public GraphicsPage(OptionContext context, HiResResolutionEditor? hiResEditor)
			: base(OptionCatalog.Page("graphics"), context)
		{
			GameConfiguration configuration = context.Configuration;

			_mode = Catalog.Get<ChoiceOption>("mode");
			_mode.IndexFromRaw = raw => IniValue.ToInt32(raw) is >= 1 and <= 6 ? IniValue.ToInt32(raw) : 0;
			_mode.Written = raw =>
			{
				// The mode is kept in both files, as the original tool did.
				configuration.Ddraw.SetValue("Graphics", "Mode", raw, IniWriteMode.AddIfMissing);
				configuration.Sfall?.SetValue("Graphics", "Mode", raw, IniWriteMode.AddIfMissing);
			};

			Resolution = new SfallResolutionEditor(Catalog.Get<NumberOption>("width"), Catalog.Get<NumberOption>("height"), hiResEditor, context.Services.DisplayModes);
			_mode.PropertyChanged += (_, _) => Resolution.IsScaleAvailable = _mode.SelectedIndex > 0;

			BoolOption textureFilter = Catalog.Get<BoolOption>("textureFilter");
			textureFilter.Interpret = raw => raw == "2";

			_globalShaderFile = Catalog.Get<TextOption>("globalShaderFile");
			_globalShaderFile.Suggestions = FindShaders(configuration);

			Groups = Catalog.BuildGroups(new Dictionary<String, Object> { ["resolution"] = Resolution });
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