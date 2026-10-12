namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows.Input;
	using Infrastructure;
	using Localization;
	using Services;

	/// <summary>
	/// An entry of a menu of the title bar: its text, whether it is the current choice, and what choosing it does.
	/// </summary>
	internal sealed class MenuChoice : ObservableObject
	{
		private readonly Func<Boolean> _isSelected;
		private readonly Func<String> _text;

		public MenuChoice(Func<String> text, Func<Boolean> isSelected, Action choose)
		{
			_text = text;
			_isSelected = isSelected;
			ChooseCommand = new RelayCommand(choose);
		}

		public String Text => _text();

		public Boolean IsSelected => _isSelected();

		public ICommand ChooseCommand { get; }

		/// <summary>
		/// Tells the menu to show the state again, after a choice was made.
		/// </summary>
		public void Refresh()
		{
			OnPropertyChanged(nameof(IsSelected));
			OnPropertyChanged(nameof(Text));
		}
	}

	/// <summary>
	/// The choices of the theme menu: Automatic, Light and Dark.
	/// </summary>
	internal static class ThemeChoices
	{
		public static IReadOnlyList<MenuChoice> Create(IThemeService theme)
		{
			List<MenuChoice> choices = [];
			foreach (ThemePreference preference in Enum.GetValues<ThemePreference>())
			{
				choices.Add(new MenuChoice(
					() => Localizer.T(preference.ToString()),
					() => theme.Preference == preference,
					() =>
					{
						theme.SetPreference(preference);
						choices.ForEach(choice => choice.Refresh());
					}));
			}

			return choices;
		}
	}

	/// <summary>
	/// The choices of the language menu: one per language of the program, named in its own language.
	/// </summary>
	internal static class LanguageChoices
	{
		public static IReadOnlyList<MenuChoice> Create(IAppHost host)
		{
			return Localizer.Languages
				.Select(language => new MenuChoice(() => language.NativeName, () => language.Code == Localizer.Instance.Language, () => host.ChangeLanguage(language.Code)))
				.ToList();
		}
	}
}