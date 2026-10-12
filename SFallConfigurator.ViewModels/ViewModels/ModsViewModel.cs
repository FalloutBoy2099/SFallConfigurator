namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Windows.Input;
	using Configuration;
	using Infrastructure;
	using Localization;
	using Mods;
	using Options;

	/// <summary>
	/// The "Mods" tab: the load order of the mods and the mods that load automatically.
	/// </summary>
	internal sealed class ModsViewModel : ObservableObject
	{
		private readonly ModsManager _manager;
		private readonly OptionContext _context;

		private ModEntry? _selectedMod;
		private String? _selectedAvailableMod;

		public ModsViewModel(OptionContext context, Action<String> openFile)
		{
			_context = context;
			_manager = new ModsManager(context.Configuration.GamePath);

			DisableAutoloading = new CatalogOptions(OptionCatalog.Page("mods"), context).Get<BoolOption>("disableAutoloading");
			DisableAutoloading.Interpret = raw => IniValue.ToInt32(raw) == 0;

			AddCommand = new RelayCommand(AddSelected, () => SelectedAvailableMod is not null);
			RemoveCommand = new RelayCommand(RemoveSelected, () => SelectedMod is not null);
			MoveUpCommand = new RelayCommand(() => MoveSelected(-1), () => CanMoveSelected(-1));
			MoveDownCommand = new RelayCommand(() => MoveSelected(1), () => CanMoveSelected(1));
			CreateFileCommand = new RelayCommand(CreateOrderFile, () => !OrderFileExists);
			OpenFileCommand = new RelayCommand(() => openFile(_manager.OrderFileRelativePath), () => OrderFileExists);
		}

		public ObservableCollection<ModEntry> Mods => _manager.Mods;

		public ObservableCollection<String> AvailableMods => _manager.AvailableMods;

		/// <summary>
		/// The sfall option that stops mods outside the load order from loading.
		/// </summary>
		public BoolOption DisableAutoloading { get; }

		public ICommand AddCommand { get; }

		public ICommand RemoveCommand { get; }

		public ICommand MoveUpCommand { get; }

		public ICommand MoveDownCommand { get; }

		public ICommand CreateFileCommand { get; }

		public ICommand OpenFileCommand { get; }

		public String Help => Localizer.T("- The lower mods of the list have priority over the upper ones: the files of a lower mod\noverride the files of the upper mods.\n\n- A gray check box means that the file(s) or the folder of the mod are missing.");

		public Boolean OrderFileExists => _manager.OrderFileExists;

		public Boolean IsModified => _manager.IsModified;

		/// <summary>
		/// The load order as it is in the file, one line per mod.
		/// </summary>
		public IReadOnlyList<String> SavedOrder => _manager.SavedOrder;

		/// <summary>
		/// The load order as it is now, one line per mod.
		/// </summary>
		public IReadOnlyList<String> CurrentOrder => _manager.CurrentOrder;

		public ModEntry? SelectedMod
		{
			get => _selectedMod;
			set => SetProperty(ref _selectedMod, value);
		}

		public String? SelectedAvailableMod
		{
			get => _selectedAvailableMod;
			set => SetProperty(ref _selectedAvailableMod, value);
		}

		public void Load()
		{
			_manager.Load();
			LoadOptions();
			OnPropertyChanged(nameof(OrderFileExists));
		}

		/// <summary>
		/// Reads the options of the tab again, and keeps the load order the user is editing.
		/// </summary>
		public void LoadOptions()
		{
			DisableAutoloading.Load();
		}

		public void Save()
		{
			_manager.Save();
		}

		/// <summary>
		/// Writes the load order next to its file, to be replaced together with the other files. Null when nothing changed.
		/// </summary>
		public StagedFile? StageSave()
		{
			return _manager.Stage();
		}

		/// <summary>
		/// Moves a mod to another place of the load order (used by drag and drop).
		/// </summary>
		public void MoveMod(Int32 fromIndex, Int32 toIndex)
		{
			if (fromIndex < 0 || toIndex < 0 || fromIndex >= Mods.Count || toIndex >= Mods.Count)
			{
				return;
			}

			ModEntry moved = Mods[fromIndex];
			_manager.Move(fromIndex, toIndex);
			SelectedMod = moved;
		}

		private void AddSelected()
		{
			if (SelectedAvailableMod is null)
			{
				return;
			}

			// The mod goes in front of the selected one; behind it when the last one is selected.
			Int32 selected = SelectedMod is null ? -1 : Mods.IndexOf(SelectedMod);
			Int32 index = selected < 0 || selected == Mods.Count - 1 ? Mods.Count : selected;

			Int32 availableIndex = AvailableMods.IndexOf(SelectedAvailableMod);
			_manager.Add(index, SelectedAvailableMod);
			OnPropertyChanged(nameof(OrderFileExists));

			SelectedMod = Mods[Math.Min(index, Mods.Count - 1)];
			SelectedAvailableMod = AvailableMods.Count == 0 ? null : AvailableMods[Math.Min(availableIndex, AvailableMods.Count - 1)];
		}

		private void RemoveSelected()
		{
			if (SelectedMod is null)
			{
				return;
			}

			Int32 index = Mods.IndexOf(SelectedMod);
			_manager.Remove(index);
			SelectedMod = Mods.Count == 0 ? null : Mods[Math.Min(index, Mods.Count - 1)];
		}

		private Boolean CanMoveSelected(Int32 direction)
		{
			if (SelectedMod is null)
			{
				return false;
			}

			Int32 target = Mods.IndexOf(SelectedMod) + direction;
			return target >= 0 && target < Mods.Count;
		}

		private void MoveSelected(Int32 direction)
		{
			if (SelectedMod is not null)
			{
				Int32 index = Mods.IndexOf(SelectedMod);
				MoveMod(index, index + direction);
			}
		}

		private void CreateOrderFile()
		{
			if (!ErrorGuard.Run(_context.Interaction, _manager.CreateOrderFile))
			{
				// The list was rebuilt before the file failed to be written: show what is on the disk again.
				_manager.Load();
			}

			OnPropertyChanged(nameof(OrderFileExists));
		}
	}
}