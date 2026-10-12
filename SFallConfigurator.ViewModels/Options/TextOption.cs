namespace SFallConfigurator.Options
{
	using System;
	using System.Collections.Generic;
	using System.Windows.Input;
	using Configuration;
	using Infrastructure;
	using Localization;

	/// <summary>
	/// A free text setting. With suggestions it is shown as an editable drop-down list.
	/// </summary>
	internal class TextOption : ConfigOption
	{
		protected override Boolean IsKeptInProfiles => false;

		internal override String InitialRaw => KnownDefault ?? DefaultValue ?? String.Empty;

		private String _text = String.Empty;
		private Boolean _isReadOnly;
		private IReadOnlyList<String>? _suggestions;
		private String? _actionToolTip;
		private String? _secondaryActionToolTip;

		public TextOption(OptionLocation location, String key, String label)
			: base(location, key, label)
		{
		}

		/// <summary>
		/// Known values offered in the drop-down list.
		/// </summary>
		public IReadOnlyList<String>? Suggestions
		{
			get => _suggestions;
			set => SetProperty(ref _suggestions, value);
		}

		/// <summary>
		/// Adjusts the text before it is stored (for example to lower case).
		/// </summary>
		public Func<String, String>? Normalize { get; set; }

		/// <summary>
		/// Caption of the optional button next to the text box.
		/// </summary>
		public String ActionLabel { get; set; } = "...";

		public String? ActionToolTip
		{
			get => _actionToolTip is null ? null : Localizer.T(_actionToolTip);
			set => _actionToolTip = value;
		}

		/// <summary>
		/// The command of the optional button next to the text box; the button is hidden without it.
		/// </summary>
		public ICommand? ActionCommand { get; set; }

		/// <summary>
		/// A second optional button, for settings that accept a file or a folder.
		/// </summary>
		public String SecondaryActionLabel { get; set; } = "...";

		public String? SecondaryActionToolTip
		{
			get => _secondaryActionToolTip is null ? null : Localizer.T(_secondaryActionToolTip);
			set => _secondaryActionToolTip = value;
		}

		public ICommand? SecondaryActionCommand { get; set; }

		public Boolean IsReadOnly
		{
			get => _isReadOnly;
			set => SetProperty(ref _isReadOnly, value);
		}

		public String Text
		{
			get => _text;
			set
			{
				if (_isReadOnly && !IsLoading)
				{
					OnPropertyChanged();
					return;
				}

				String adjusted = Normalize is null || IsLoading ? value ?? String.Empty : Normalize(value ?? String.Empty);
				if (_text == adjusted)
				{
					return;
				}

				_text = adjusted;
				OnPropertyChanged();
				OnValueChanged(_isReadOnly ? null : adjusted.Trim());
			}
		}

		/// <summary>
		/// Shows <paramref name="text"/> without storing it (a value imposed by another option).
		/// </summary>
		public void ShowFixed(String text, Boolean readOnly)
		{
			IsReadOnly = readOnly;
			if (_text != text)
			{
				_text = text;
				OnPropertyChanged(nameof(Text));
			}
		}

		protected override void ApplyValue(String? raw)
		{
			Text = raw ?? String.Empty;
		}

		protected override Object? GetSnapshot()
		{
			return _text;
		}
	}

	/// <summary>
	/// What a <see cref="PathOption"/> lets the user browse for.
	/// </summary>
	internal enum PathKind
	{
		File,
		Folder,

		/// <summary>
		/// Both are accepted: the first button chooses a file, the second one a folder.
		/// </summary>
		FileOrFolder,
	}

	/// <summary>
	/// A text setting that holds a game relative path, with a button to browse for it.
	/// </summary>
	internal sealed class PathOption : TextOption
	{
		private readonly String _gamePath;
		private readonly IUserInteraction _interaction;

		public PathOption(OptionLocation location, String key, String label, PathKind kind, String gamePath, IUserInteraction interaction)
			: base(location, key, label)
		{
			Kind = kind;
			_gamePath = gamePath;
			_interaction = interaction;

			Boolean acceptsFile = kind != PathKind.Folder;
			Boolean acceptsFolder = kind != PathKind.File;
			if (acceptsFile)
			{
				ActionCommand = new RelayCommand(BrowseFile);
				ActionLabel = "...";
				ActionToolTip = "Select a file.";
			}

			if (acceptsFolder)
			{
				// With only a folder to choose, the folder button is the main button.
				if (acceptsFile)
				{
					SecondaryActionCommand = new RelayCommand(BrowseFolder);
					SecondaryActionLabel = "\uE838"; // folder glyph of the symbol font
					SecondaryActionToolTip = "Select a folder.";
				}
				else
				{
					ActionCommand = new RelayCommand(BrowseFolder);
					ActionLabel = "...";
					ActionToolTip = "Select a folder.";
				}
			}
		}

		public PathKind Kind { get; }

		/// <summary>
		/// Offers a list of file name filters ("Dat files|*.dat"), used when browsing for a file.
		/// </summary>
		public String FileFilter { get; set; } = "All files|*.*";

		/// <summary>
		/// Keep a trailing separator, as the game expects for the music folders.
		/// </summary>
		public Boolean TrailingSeparator { get; set; }

		private void BrowseFile()
		{
			String? initialFileName = String.IsNullOrWhiteSpace(Text) ? null : System.IO.Path.GetFileName(PathHelper.Resolve(_gamePath, Text));
			String? selected = _interaction.PickFile(FileFilter, _gamePath, initialFileName, checkFileExists: false);
			Apply(selected, trailingSeparator: false);
		}

		private void BrowseFolder()
		{
			String initial = String.IsNullOrWhiteSpace(Text) ? _gamePath : PathHelper.Resolve(_gamePath, Text);
			String? selected = _interaction.PickFolder(System.IO.Directory.Exists(initial) ? initial : _gamePath, Label);
			Apply(selected, TrailingSeparator);
		}

		private void Apply(String? selected, Boolean trailingSeparator)
		{
			if (selected is not null)
			{
				Text = PathHelper.ToGameRelative(_gamePath, selected, trailingSeparator);
			}
		}
	}
}