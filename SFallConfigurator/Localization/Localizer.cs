namespace SFallConfigurator.Localization
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Reflection;
	using System.Text.Json;
	using Configuration;
	using Infrastructure;

	/// <summary>
	/// A language of the user interface.
	/// </summary>
	internal sealed record LanguageInfo(String Code, String NativeName);

	/// <summary>
	/// A text that follows the language of the interface (used by the XAML markup extension).
	/// </summary>
	internal sealed class LocalizedText : ObservableObject
	{
		private String _value;

		public LocalizedText(String value)
		{
			_value = value;
		}

		public String Value
		{
			get => _value;
			set => SetProperty(ref _value, value);
		}
	}

	/// <summary>
	/// Translates the texts of the interface. The English text itself is the key: a text without translation is
	/// shown as it is, so English works without any dictionary and a missing translation is never an error.
	/// </summary>
	internal sealed class Localizer
	{
		private const String LanguageKey = "language";
		private const String DefaultLanguage = "en";

		private readonly Dictionary<String, LocalizedText> _observed = [];
		private Dictionary<String, String> _translations = [];

		private Localizer()
		{
		}

		private static readonly HashSet<String> _untranslated = [];

		public static Localizer Instance { get; } = new Localizer();

		/// <summary>
		/// The languages that can be chosen. English needs no dictionary.
		/// </summary>
		public static IReadOnlyList<LanguageInfo> Languages { get; } =
		[
			new LanguageInfo("en", "English"),
			new LanguageInfo("cs", "Čeština"),
			new LanguageInfo("de", "Deutsch"),
			new LanguageInfo("es", "Español"),
			new LanguageInfo("fr", "Français"),
			new LanguageInfo("hu", "Magyar"),
			new LanguageInfo("it", "Italiano"),
			new LanguageInfo("ko", "한국어"),
			new LanguageInfo("pl", "Polski"),
			new LanguageInfo("pt", "Português"),
			new LanguageInfo("ru", "Русский"),
			new LanguageInfo("sv", "Svenska"),
			new LanguageInfo("zh", "繁體中文"),
		];

		public String Language { get; private set; } = DefaultLanguage;

		/// <summary>
		/// Translates a text into the current language.
		/// </summary>
		public static String T(String text)
		{
			if (Instance._translations.TryGetValue(text, out String? translated))
			{
				return translated;
			}

			if (Instance.Language != DefaultLanguage)
			{
				lock (_untranslated)
				{
					_untranslated.Add(text);
				}
			}

			return text;
		}

		/// <summary>
		/// Translates a text that may also be a message of the system (an exception): it is not reported as missing.
		/// </summary>
		public static String TranslateIfKnown(String text)
		{
			return Instance._translations.TryGetValue(text, out String? translated) ? translated : text;
		}

		/// <summary>
		/// The texts that were asked for in another language than English and are not in its dictionary. The tests check it.
		/// </summary>
		internal static IReadOnlyList<String> GetUntranslated()
		{
			lock (_untranslated)
			{
				return [.. _untranslated];
			}
		}

		internal static void ClearUntranslated()
		{
			lock (_untranslated)
			{
				_untranslated.Clear();
			}
		}

		/// <summary>
		/// Translates a format and fills it.
		/// </summary>
		public static String F(String format, params Object[] arguments)
		{
			return String.Format(CultureInfo.CurrentCulture, T(format), arguments);
		}

		/// <summary>
		/// The language of the operating system when it is one of ours, English otherwise.
		/// </summary>
		public static String DetectSystemLanguage()
		{
			return Normalize(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
		}

		/// <summary>
		/// Starts with the language the user chose before, or with the one of the operating system.
		/// </summary>
		public void Initialize()
		{
			String? remembered = UserSettings.Get(LanguageKey);
			Apply(remembered is not null && Languages.Any(language => language.Code == remembered) ? remembered : DetectSystemLanguage());
		}

		/// <summary>
		/// Changes the language and remembers the choice.
		/// </summary>
		public void SetLanguage(String code)
		{
			Apply(Normalize(code));
			UserSettings.Set(LanguageKey, Language);
		}

		/// <summary>
		/// A text object that updates itself when the language changes.
		/// </summary>
		public LocalizedText Observe(String text)
		{
			if (!_observed.TryGetValue(text, out LocalizedText? observed))
			{
				observed = new LocalizedText(T(text));
				_observed[text] = observed;
			}

			return observed;
		}

		/// <summary>
		/// Applies a language without remembering it (used by the tests).
		/// </summary>
		internal void Apply(String code)
		{
			Language = Normalize(code);
			_translations = Language == DefaultLanguage ? [] : LoadDictionary(Language);

			foreach ((String text, LocalizedText observed) in _observed)
			{
				observed.Value = T(text);
			}
		}

		private static String Normalize(String code)
		{
			String lower = code.ToLowerInvariant();
			return Languages.Any(language => language.Code == lower) ? lower : DefaultLanguage;
		}

		private static Dictionary<String, String> LoadDictionary(String code)
		{
			String resource = "SFallConfigurator.Localization." + code + ".json";
			using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource);
			if (stream is null)
			{
				return [];
			}

			return JsonSerializer.Deserialize<Dictionary<String, String>>(stream) ?? [];
		}
	}
}