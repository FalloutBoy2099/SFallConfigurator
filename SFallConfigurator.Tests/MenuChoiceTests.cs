namespace SFallConfigurator.Tests
{
	using System;
	using System.Linq;
	using Infrastructure;
	using Localization;
	using Services;
	using ViewModels;
	using Xunit;

	public sealed class MenuChoiceTests
	{
		/// <summary>
		/// A theme service that remembers nothing on the disk.
		/// </summary>
		private sealed class FakeThemeService : IThemeService
		{
			public ThemePreference Preference { get; private set; } = ThemePreference.Automatic;

			public void SetPreference(ThemePreference preference)
			{
				Preference = preference;
			}
		}

		[Fact]
		public void TheThemeMenuHasOneEntryPerPreferenceAndMarksTheCurrentOne()
		{
			FakeThemeService theme = new();

			System.Collections.Generic.IReadOnlyList<MenuChoice> choices = ThemeChoices.Create(theme);

			Assert.Equal(3, choices.Count);
			Assert.Equal([true, false, false], choices.Select(choice => choice.IsSelected));
		}

		[Fact]
		public void ChoosingAThemeChangesItAndMovesTheMark()
		{
			Localizer.Instance.Apply("en");
			FakeThemeService theme = new();
			System.Collections.Generic.IReadOnlyList<MenuChoice> choices = ThemeChoices.Create(theme);

			choices.Single(choice => choice.Text == "Dark").ChooseCommand.Execute(null);

			Assert.Equal(ThemePreference.Dark, theme.Preference);
			Assert.Equal(["Automatic", "Light", "Dark"], choices.Select(choice => choice.Text));
			Assert.Equal([false, false, true], choices.Select(choice => choice.IsSelected));
		}

		[Fact]
		public void TheLanguageMenuListsEveryLanguageInItsOwnNameAndAsksTheHostToSwitch()
		{
			Localizer.Instance.Apply("fr");
			try
			{
				TestHost host = new();
				System.Collections.Generic.IReadOnlyList<MenuChoice> choices = LanguageChoices.Create(host);

				Assert.Equal(Localizer.Languages.Count, choices.Count);
				Assert.Equal(["Français"], choices.Where(choice => choice.IsSelected).Select(choice => choice.Text));

				choices.Single(choice => choice.Text == "Deutsch").ChooseCommand.Execute(null);

				Assert.Equal(["de"], host.ChangedLanguages);
			}
			finally
			{
				Localizer.Instance.Apply("en");
			}
		}
	}
}