namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Windows.Input;
	using Infrastructure;
	using Localization;
	using Options;
	using Services;

	/// <summary>
	/// The profiles of the user: a profile keeps the values of the options, to put them back later or in another game folder.
	/// </summary>
	internal sealed class ProfilesViewModel : ObservableObject
	{
		private readonly ProfileStore _store;
		private readonly Func<IEnumerable<ConfigOption>> _options;
		private readonly IUserInteraction _interaction;

		private IReadOnlyList<MenuChoice> _applyChoices = [];
		private IReadOnlyList<MenuChoice> _deleteChoices = [];

		public ProfilesViewModel(ProfileStore store, Func<IEnumerable<ConfigOption>> options, IUserInteraction interaction)
		{
			_store = store;
			_options = options;
			_interaction = interaction;
			SaveCommand = new RelayCommand(Save);
			Refresh();
		}

		public ICommand SaveCommand { get; }

		/// <summary>
		/// One entry per profile: choosing it applies the profile.
		/// </summary>
		public IReadOnlyList<MenuChoice> ApplyChoices => _applyChoices;

		/// <summary>
		/// One entry per profile: choosing it deletes the profile.
		/// </summary>
		public IReadOnlyList<MenuChoice> DeleteChoices => _deleteChoices;

		public Boolean HasProfiles => _applyChoices.Count > 0;

		/// <summary>
		/// Reads the list of the profiles again.
		/// </summary>
		public void Refresh()
		{
			IReadOnlyList<String> names = ErrorGuard.Try(_store.GetNames, []);
			_applyChoices = names.Select(name => new MenuChoice(() => name, () => false, () => Apply(name))).ToList();
			_deleteChoices = names.Select(name => new MenuChoice(() => name, () => false, () => Delete(name))).ToList();
			OnPropertyChanged(nameof(ApplyChoices));
			OnPropertyChanged(nameof(DeleteChoices));
			OnPropertyChanged(nameof(HasProfiles));
		}

		/// <summary>
		/// The values of the options that the user may carry from one game folder to another.
		/// </summary>
		public Profile Capture(String name)
		{
			Dictionary<String, String> values = new(StringComparer.OrdinalIgnoreCase);
			foreach (ConfigOption option in _options())
			{
				if (option.ProfileKey is { } key && option.CurrentRaw is { } raw)
				{
					values.TryAdd(key, raw);
				}
			}

			return new Profile(name, values);
		}

		/// <summary>
		/// Gives the options the values of the profile. Returns how many options changed; nothing is saved to the game files.
		/// </summary>
		public Int32 Apply(String name)
		{
			Profile? profile = null;
			if (!ErrorGuard.Run(_interaction, () => profile = _store.Load(name)) || profile is null)
			{
				return 0;
			}

			Int32 changed = 0;
			foreach (ConfigOption option in _options())
			{
				if (option.ProfileKey is { } key && profile.Values.TryGetValue(key, out String? raw) && option.ApplyRaw(raw))
				{
					changed++;
				}
			}

			_interaction.ShowMessage(
				changed == 0
					? Localizer.F("The profile {0} changes nothing: the settings already have its values.", name)
					: Localizer.F("The profile {0} was applied: {1} settings changed. Nothing is saved until you save.", name, changed));
			return changed;
		}

		private void Save()
		{
			String? entered = _interaction.PromptText("Name of the profile:", null, "Save the settings as a profile");
			String name = ProfileStore.CleanName(entered ?? String.Empty);
			if (name.Length == 0)
			{
				return;
			}

			if (ErrorGuard.Try(() => _store.Exists(name), false) && !_interaction.Confirm(Localizer.F("The profile {0} exists already. Replace it?", name)))
			{
				return;
			}

			Profile profile = Capture(name);
			if (ErrorGuard.Run(_interaction, () => _store.Save(profile)))
			{
				Refresh();
				_interaction.ShowMessage(Localizer.F("The profile {0} was saved ({1} settings).", name, profile.Values.Count));
			}
		}

		private void Delete(String name)
		{
			if (_interaction.Confirm(Localizer.F("Delete the profile {0}?", name)) && ErrorGuard.Run(_interaction, () => _store.Delete(name)))
			{
				Refresh();
			}
		}
	}
}