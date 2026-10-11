namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Windows.Input;
	using Dat;
	using Infrastructure;
	using Localization;

	/// <summary>
	/// A DAT archive of the game folder and its description.
	/// </summary>
	internal sealed class DatFileInfo : ObservableObject
	{
		private String _description;

		public DatFileInfo(String path, String description)
		{
			FullPath = path;
			_description = description;
		}

		/// <summary>
		/// Full path of the archive.
		/// </summary>
		public String FullPath { get; }

		public String Name => Path.GetFileName(FullPath);

		public String Description
		{
			get => _description;
			set => SetProperty(ref _description, value);
		}
	}

	/// <summary>
	/// The "Dat Info" tab: lists the DAT archives of the game and edits their descriptions.
	/// </summary>
	internal sealed class DatInfoViewModel : ObservableObject
	{
		private const String NoDescription = "<no description>";

		private readonly String _gamePath;
		private readonly IUserInteraction _interaction;

		private DatFileInfo? _selected;

		public DatInfoViewModel(String gamePath, IUserInteraction interaction)
		{
			_gamePath = gamePath;
			_interaction = interaction;
			EditDescriptionCommand = new RelayCommand(EditDescription, () => Selected is not null);
		}

		public ObservableCollection<DatFileInfo> Files { get; } = [];

		public ICommand EditDescriptionCommand { get; }

		public DatFileInfo? Selected
		{
			get => _selected;
			set => SetProperty(ref _selected, value);
		}

		/// <summary>
		/// Reads the archives of the game folder and of its "mods" folder again.
		/// </summary>
		public async Task RefreshAsync()
		{
			IReadOnlyList<DatFileInfo> files = await Task.Run(ReadArchives);

			Files.Clear();
			foreach (DatFileInfo file in files)
			{
				Files.Add(file);
			}
		}

		private IReadOnlyList<DatFileInfo> ReadArchives()
		{
			List<String> paths = Directory.GetFiles(_gamePath, "*.dat", SearchOption.TopDirectoryOnly).ToList();
			String modsPath = Path.Combine(_gamePath, "mods");
			if (Directory.Exists(modsPath))
			{
				paths.AddRange(Directory.GetFiles(modsPath, "*.dat", SearchOption.TopDirectoryOnly));
			}

			List<DatFileInfo> files = [];
			foreach (String path in paths)
			{
				try
				{
					files.Add(new DatFileInfo(path, DatDescription.Read(path) ?? Localizer.T(NoDescription)));
				}
				catch (Exception exception) when (exception is DatFormatException or IOException or UnauthorizedAccessException)
				{
					// Not a readable Fallout 2 archive (for example a Fallout 1 DAT): it is left out.
				}
			}

			return files;
		}

		private void EditDescription()
		{
			if (Selected is null)
			{
				return;
			}

			String? description = _interaction.PromptText(Localizer.F("Enter a description for {0}", Selected.Name), Selected.Description);
			if (String.IsNullOrEmpty(description) || description == Selected.Description)
			{
				return;
			}

			try
			{
				DatDescription.Write(Selected.FullPath, description);
				Selected.Description = description;
			}
			catch (Exception exception) when (exception is DatFormatException or IOException or UnauthorizedAccessException)
			{
				_interaction.ShowError(exception.Message);
			}
		}
	}
}