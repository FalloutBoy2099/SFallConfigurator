namespace SFallConfigurator.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Text.Json;
	using Configuration;
	using Localization;
	using Views;
	using Xunit;

	/// <summary>
	/// The language and the user settings are shared state: these tests must not run in parallel with the others that use them.
	/// </summary>
	public sealed class LocalizationTests : IDisposable
	{
		private readonly TestFolder _settings = new TestFolder();

		public LocalizationTests()
		{
			UserSettings.DirectoryOverride = _settings.Path;
		}

		public void Dispose()
		{
			Localizer.Instance.Apply("en");
			UserSettings.DirectoryOverride = null;
			_settings.Dispose();
		}

		[Fact]
		public void EnglishTextsAreShownAsTheyAre()
		{
			Localizer.Instance.Apply("en");

			Assert.Equal("Save the settings and close.", Localizer.T("Save the settings and close."));
			Assert.Equal("A text nobody translated", Localizer.T("A text nobody translated"));
		}

		[Theory]
		[InlineData("fr", "Enregistre les réglages et ferme le programme.")]
		[InlineData("ru", "Сохранить настройки и закрыть.")]
		public void TextsAreTranslated(String language, String expected)
		{
			Localizer.Instance.Apply(language);

			Assert.Equal(expected, Localizer.T("Save the settings and close."));
		}

		[Fact]
		public void AMissingTranslationFallsBackToEnglish()
		{
			Localizer.Instance.Apply("fr");

			Assert.Equal("A text nobody translated", Localizer.T("A text nobody translated"));
		}

		[Fact]
		public void FormatsAreTranslatedThenFilled()
		{
			Localizer.Instance.Apply("fr");

			Assert.Equal("Enregistre les réglages et lance le jeu (FALLOUT2.EXE).", Localizer.F("Save the settings and start the game ({0}).", "FALLOUT2.EXE"));
		}

		[Fact]
		public void UnknownLanguagesAreEnglish()
		{
			Localizer.Instance.Apply("xx");

			Assert.Equal("en", Localizer.Instance.Language);
		}

		[Fact]
		public void ObservedTextsFollowTheLanguage()
		{
			Localizer.Instance.Apply("en");
			LocalizedText text = Localizer.Instance.Observe("Yes");
			Assert.Equal("Yes", text.Value);

			Localizer.Instance.Apply("fr");

			Assert.Equal("Oui", text.Value);
		}

		[Fact]
		public void TheChosenLanguageIsRememberedAndRestored()
		{
			Localizer.Instance.SetLanguage("fr");
			Localizer.Instance.Apply("en");

			Localizer.Instance.Initialize();

			Assert.Equal("fr", Localizer.Instance.Language);
		}

		[Fact]
		public void WithoutAChoiceTheLanguageOfTheSystemIsUsedOrEnglish()
		{
			CultureInfo previous = CultureInfo.CurrentUICulture;
			try
			{
				CultureInfo.CurrentUICulture = new CultureInfo("ru-RU");
				Assert.Equal("ru", Localizer.DetectSystemLanguage());

				CultureInfo.CurrentUICulture = new CultureInfo("ja-JP");
				Assert.Equal("en", Localizer.DetectSystemLanguage());

				Localizer.Instance.Initialize();
				Assert.Equal("en", Localizer.Instance.Language);
			}
			finally
			{
				CultureInfo.CurrentUICulture = previous;
			}
		}

		private static Dictionary<String, String> ReadDictionary(String language)
		{
			using Stream stream = typeof(Localizer).Assembly.GetManifestResourceStream("SFallConfigurator.Localization." + language + ".json")!;
			return JsonSerializer.Deserialize<Dictionary<String, String>>(stream)!;
		}

		[Fact]
		public void TheFrenchDictionaryCoversEverythingTheRussianOneDoes()
		{
			Dictionary<String, String> french = ReadDictionary("fr");
			Dictionary<String, String> russian = ReadDictionary("ru");

			Assert.NotEmpty(french);
			Assert.NotEmpty(russian);
			Assert.All(russian.Keys, key => Assert.True(french.ContainsKey(key), "Missing in fr.json: " + key));
			Assert.All(french.Where(entry => entry.Value.Contains("{0}")), entry => Assert.Contains("{0}", entry.Key));
		}

		[Fact]
		public void EveryLanguageOfTheMenuHasACompleteDictionary()
		{
			Dictionary<String, String> french = ReadDictionary("fr");

			foreach (LanguageInfo language in Localizer.Languages.Where(language => language.Code != "en"))
			{
				Dictionary<String, String> dictionary = ReadDictionary(language.Code);
				if (language.Code != "ru")
				{
					Assert.Empty(french.Keys.Except(dictionary.Keys));
					Assert.Empty(dictionary.Keys.Except(french.Keys));
				}

				Assert.All(dictionary, entry =>
				{
					Assert.False(String.IsNullOrWhiteSpace(entry.Value), language.Code + ": empty translation of " + entry.Key);
					Assert.Equal(entry.Key.Contains("{0}"), entry.Value.Contains("{0}"));
				});
			}
		}

		[Fact]
		public void NoTranslationLooksLikeMarkupInsteadOfText()
		{
			foreach (LanguageInfo language in Localizer.Languages.Where(language => language.Code != "en"))
			{
				Assert.All(ReadDictionary(language.Code), entry =>
					Assert.False(
						System.Text.RegularExpressions.Regex.IsMatch(entry.Value, "^\\s*(ToolTip|Header|Content|Text|Title)\\s*=\\s*\""),
						language.Code + ": " + entry.Key));
			}
		}

		[Fact]
		public void TheLanguageMenuListsTheLanguagesOfTheRestorationProject()
		{
			String[] expected = ["cs", "de", "en", "es", "fr", "hu", "it", "ko", "pl", "pt", "ru", "sv", "zh"];

			Assert.Equal(expected, Localizer.Languages.Select(language => language.Code).Order());
		}

		[Theory]
		[InlineData("de", "Hell")]
		[InlineData("pl", "Jasny")]
		[InlineData("cs", "Světlý")]
		[InlineData("hu", "Világos")]
		[InlineData("zh", "淺色")]
		public void TheThemeNamesAreTranslated(String language, String expected)
		{
			Localizer.Instance.Apply(language);

			Assert.Equal(expected, Localizer.T("Light"));
		}

		[Theory]
		[InlineData(null, "Automatic")]
		[InlineData("", "Automatic")]
		[InlineData("dark", "Dark")]
		[InlineData("Light", "Light")]
		[InlineData("purple", "Automatic")]
		[InlineData("7", "Automatic")]
		public void TheThemePreferenceIsReadFromTheSettings(String? text, String expected)
		{
			Assert.Equal(expected, ThemeManager.Parse(text).ToString());
		}

		[Fact]
		public void SettingsKeepOtherKeysAndRepeatedValues()
		{
			UserSettings.SetAll("game_path", ["one", "two"]);
			UserSettings.Set("language", "fr");
			UserSettings.SetAll("game_path", ["three"]);

			Assert.Equal(["three"], UserSettings.GetAll("game_path"));
			Assert.Equal("fr", UserSettings.Get("language"));

			UserSettings.Set("language", null);
			Assert.Null(UserSettings.Get("language"));
			Assert.Equal(["three"], UserSettings.GetAll("game_path"));
		}

		[Fact]
		public void WindowPlacementIsRememberedAndKeptOnScreen()
		{
			Assert.Null(WindowPlacement.Load());

			new WindowPlacement(100.5, 50, 640, 700, IsMaximized: true).Save();
			WindowPlacement? loaded = WindowPlacement.Load();

			Assert.Equal(new WindowPlacement(100.5, 50, 640, 700, true), loaded);

			// A window saved on a screen that is gone is moved back into the visible area.
			WindowPlacement fitted = new WindowPlacement(3000, -500, 640, 700, false).FitInto(0, 0, 1920, 1080);
			Assert.Equal(new WindowPlacement(1280, 0, 640, 700, false), fitted);

			WindowPlacement shrunk = new WindowPlacement(0, 0, 4000, 3000, false).FitInto(0, 0, 1920, 1080);
			Assert.Equal(new WindowPlacement(0, 0, 1920, 1080, false), shrunk);
		}
	}
}