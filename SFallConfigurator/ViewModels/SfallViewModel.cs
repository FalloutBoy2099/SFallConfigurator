namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Windows.Input;
	using Configuration;
	using Infrastructure;

	/// <summary>
	/// The sfall configuration tab: the pages of options and the actions on the configuration files.
	/// </summary>
	internal sealed class SfallViewModel : ObservableObject
	{
		private readonly OptionContext _context;

		public SfallViewModel(OptionContext context, HiResViewModel hiRes, Action<String> openFile)
		{
			_context = context;

			MergeCommand = new RelayCommand(Merge);
			MakeSfallIniCommand = new RelayCommand(MakeSfallIni, () => CanMakeSfallIni);
			OpenConfigCommand = new RelayCommand(() => openFile(GetPrimaryConfigName()));
			OpenModsIniCommand = new RelayCommand(() => openFile(context.Configuration.SfallModsFileName), () => context.Configuration.SfallMods is not null);

			SystemTab = new SystemPage(context, new RelayCommand(ChooseSfallIni));
			GraphicsTab = new GraphicsPage(context, hiRes.Graphics.Resolution);

			Pages =
			[
				new CommonPage(context),
				new CombatPage(context),
				new InventoryPage(context),
				new InterfacePage(context),
				new MiscPage(context),
				GraphicsTab,
				SystemTab,
			];
		}

		public IReadOnlyList<OptionPage> Pages { get; }

		public SystemPage SystemTab { get; }

		public GraphicsPage GraphicsTab { get; }

		public ICommand MergeCommand { get; }

		public ICommand MakeSfallIniCommand { get; }

		public ICommand OpenConfigCommand { get; }

		public ICommand OpenModsIniCommand { get; }

		/// <summary>
		/// sfall.ini can be created while there is no custom sfall configuration file.
		/// </summary>
		public Boolean CanMakeSfallIni => _context.Configuration.Sfall is null && _context.Configuration.DdrawExists;

		/// <summary>
		/// Reads every option from the configuration.
		/// </summary>
		public void Load()
		{
			foreach (OptionPage page in Pages)
			{
				page.Load();
			}

			OnPropertyChanged(nameof(CanMakeSfallIni));
		}

		/// <summary>
		/// Updates what depends on the game executable.
		/// </summary>
		public void RefreshExeState()
		{
			SystemTab.RefreshFiles();
		}

		private String GetPrimaryConfigName()
		{
			return _context.Configuration.SfallFileName ?? Path.GetFileName(_context.Configuration.Ddraw.Path);
		}

		private void Merge()
		{
			IUserInteraction interaction = _context.Interaction;
			String? path = interaction.PickFile("ini files|*.ini", _context.Configuration.GamePath);
			if (path is null)
			{
				return;
			}

			Boolean merged = ErrorGuard.Run(interaction, () =>
			{
				ConfigMerger.Merge(_context.Configuration.Ddraw, path, carryModifiedIni: true);
				if (_context.Configuration.Sfall is not null)
				{
					ConfigMerger.Merge(_context.Configuration.Sfall, path, carryModifiedIni: false);
				}
			});

			Load();
			if (merged)
			{
				interaction.ShowMessage("The settings were transferred successfully!");
			}
		}

		private void MakeSfallIni()
		{
			if (ErrorGuard.Run(_context.Interaction, _context.Configuration.CreateSfallIni))
			{
				Load();
			}
		}

		private void ChooseSfallIni()
		{
			String? path = _context.Interaction.PickFile("ini files|*.ini", _context.Configuration.GamePath);
			if (path is null)
			{
				return;
			}

			if (ErrorGuard.Run(_context.Interaction, () => _context.Configuration.SetCustomSfallFile(path)))
			{
				Load();
			}
		}
	}
}