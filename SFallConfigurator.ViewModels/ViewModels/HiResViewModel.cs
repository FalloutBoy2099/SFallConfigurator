namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Windows.Input;
	using Configuration;
	using Infrastructure;
	using Services;

	/// <summary>
	/// Which Hi-Res patch the game uses.
	/// </summary>
	internal enum HiResMode
	{
		None,

		/// <summary>
		/// The patch built into sfall (version 5 and later).
		/// </summary>
		Sfall,

		/// <summary>
		/// Mash's Hi-Res patch, loaded through a patch of the game executable.
		/// </summary>
		Mash,
	}

	/// <summary>
	/// Choice between the Hi-Res patches, and the button that applies it to the executable.
	/// </summary>
	internal sealed class HiResModePanel : ObservableObject
	{
		private readonly OptionContext _context;

		private Boolean _isSfallAvailable;
		private Boolean _isMashAvailable;
		private HiResMode _selectedMode;
		private HiResMode _activeMode;

		public HiResModePanel(OptionContext context)
		{
			_context = context;
			ApplyCommand = new RelayCommand(Apply, () => CanApply);
		}

		public ICommand ApplyCommand { get; }

		/// <summary>
		/// The sfall setting HiResMode exists (sfall 5 or later).
		/// </summary>
		public Boolean IsSfallAvailable
		{
			get => _isSfallAvailable;
			private set => SetProperty(ref _isSfallAvailable, value);
		}

		/// <summary>
		/// The selected executable is a genuine Fallout 2 executable that can be patched.
		/// </summary>
		public Boolean IsMashAvailable
		{
			get => _isMashAvailable;
			private set => SetProperty(ref _isMashAvailable, value);
		}

		public HiResMode SelectedMode
		{
			get => _selectedMode;
			set
			{
				if (SetProperty(ref _selectedMode, value))
				{
					OnPropertyChanged(nameof(IsSfallSelected));
					OnPropertyChanged(nameof(IsMashSelected));
					OnPropertyChanged(nameof(CanApply));
				}
			}
		}

		public Boolean IsSfallSelected
		{
			get => _selectedMode == HiResMode.Sfall;
			set
			{
				if (value)
				{
					SelectedMode = HiResMode.Sfall;
				}
			}
		}

		public Boolean IsMashSelected
		{
			get => _selectedMode == HiResMode.Mash;
			set
			{
				if (value)
				{
					SelectedMode = HiResMode.Mash;
				}
			}
		}

		/// <summary>
		/// The mode that is currently applied; it is highlighted in the user interface.
		/// </summary>
		public HiResMode ActiveMode
		{
			get => _activeMode;
			private set
			{
				if (SetProperty(ref _activeMode, value))
				{
					OnPropertyChanged(nameof(IsSfallActive));
					OnPropertyChanged(nameof(IsMashActive));
					OnPropertyChanged(nameof(CanApply));
				}
			}
		}

		public Boolean IsSfallActive => _activeMode == HiResMode.Sfall;

		public Boolean IsMashActive => _activeMode == HiResMode.Mash;

		public Boolean CanApply => _selectedMode != HiResMode.None && _selectedMode != _activeMode;

		/// <summary>
		/// Reads the current state of the configuration and of the game executable.
		/// </summary>
		public void Refresh()
		{
			String exePath = _context.Configuration.GameExePath;

			String? configured = _context.Configuration.SfallSource.Read(null, "HiResMode");
			IsSfallAvailable = configured is not null;
			Boolean sfallActive = configured is not null && IniValue.ToInt32(configured) != 0;

			Boolean exists = _context.Services.FileSystem.FileExists(exePath);
			IsMashAvailable = exists && _context.Services.ExecutablePatcher.IsValid(exePath);
			Boolean mashActive = !sfallActive && IsMashAvailable && _context.Services.ExecutablePatcher.IsHiResPatchEnabled(exePath);

			HiResMode mode = sfallActive ? HiResMode.Sfall : mashActive ? HiResMode.Mash : HiResMode.None;
			ActiveMode = mode;
			SelectedMode = mode;
		}

		private void Apply()
		{
			Boolean sfall = _selectedMode == HiResMode.Sfall;
			String exePath = _context.Configuration.GameExePath;
			Boolean mash = _selectedMode == HiResMode.Mash;

			// The executable first: when it cannot be patched, the setting of sfall is not changed either, so that
			// the mode in the file is the one of the executable.
			if (_context.Services.ExecutablePatcher.IsHiResPatchEnabled(exePath) != mash && !_context.Services.ExecutablePatcher.SetHiResPatch(exePath, mash))
			{
				_context.Interaction.ShowError("The selected game executable could not be patched.");
				Refresh();
				return;
			}

			_context.Configuration.SfallSource.Write("Main", "HiResMode", sfall ? "1" : "0");
			Refresh();
		}
	}

	/// <summary>
	/// The Hi-Res patch (f2_res.dll) configuration tab.
	/// </summary>
	internal sealed class HiResViewModel : ObservableObject
	{
		private readonly OptionContext _context;

		private OptionPage? _selectedPage;

		public HiResViewModel(OptionContext context, Action<String> openFile)
		{
			_context = context;

			Mode = new HiResModePanel(context);
			Graphics = new HiResGraphicsPage(context);
			Pages = [Graphics, new HiResInterfacePage(context), new HiResScreensPage(context), new HiResMapPage(context), new HiResMiscPage(context)];
			OpenIniCommand = new RelayCommand(() => openFile(Path.GetFileName(context.Configuration.HiRes?.Path ?? "f2_res.ini")), () => IsAvailable);
		}

		public HiResModePanel Mode { get; }

		public HiResGraphicsPage Graphics { get; }

		public IReadOnlyList<OptionPage> Pages { get; }

		/// <summary>
		/// The page that is shown.
		/// </summary>
		public OptionPage? SelectedPage
		{
			get => _selectedPage;
			set => SetProperty(ref _selectedPage, value);
		}

		public ICommand OpenIniCommand { get; }

		/// <summary>
		/// f2_res.ini exists, so the Hi-Res patch settings can be edited.
		/// </summary>
		public Boolean IsAvailable => _context.Configuration.HiRes is not null;

		public void Load()
		{
			foreach (OptionPage page in Pages)
			{
				page.Load();
			}

			Mode.Refresh();
			OnPropertyChanged(nameof(IsAvailable));
		}
	}
}