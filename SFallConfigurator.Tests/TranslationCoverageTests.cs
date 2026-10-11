namespace SFallConfigurator.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.Json;
	using System.Text.RegularExpressions;
	using Localization;
	using Xunit;

	/// <summary>
	/// Every text of the program that a user can read must be in the dictionary of every language, and the dictionaries
	/// must not keep texts that nothing uses. The texts are found in the source files, so that dialogs, XAML,
	/// file filters and exception messages are covered as well as the view models.
	/// </summary>
	public sealed class TranslationCoverageTests
	{
		/// <summary>
		/// Texts that reach the dictionary lookup through a variable, so that no literal of the sources names them.
		/// </summary>
		private static readonly String[] BuiltAtRunTime = ["Automatic", "Light", "Dark"];

		private static readonly Regex StringLiteral = new("\"((?:[^\"\\\\\\r\\n]|\\\\.)*)\"", RegexOptions.Compiled);
		private static readonly Regex JoinedLiterals = new("\"\\s*\\+\\s*\"", RegexOptions.Compiled);
		private static readonly Regex LocalizedCall = new("Localizer\\s*\\.\\s*[TF]\\(\\s*\"((?:[^\"\\\\\\r\\n]|\\\\.)*)\"", RegexOptions.Compiled);
		private static readonly Regex InteractionCall = new("\\b(?:ShowMessage|ShowError|Confirm|AskSaveChanges|PromptText|PickFolder)\\(\\s*\"((?:[^\"\\\\\\r\\n]|\\\\.)*)\"(?:\\s*,\\s*\"((?:[^\"\\\\\\r\\n]|\\\\.)*)\")?", RegexOptions.Compiled);
		private static readonly Regex InteractionCaption = new(@"\b(?:ShowMessage|ShowError|Confirm|AskSaveChanges)\([^;""]*,\s*""((?:[^""\\\r\n]|\\.)*)""\s*\)", RegexOptions.Compiled);
		private static readonly Regex FilterLiteral = new("\"([^\"|\\r\\n]+)\\|\\*\\.[a-z*]+\"", RegexOptions.Compiled);
		private static readonly Regex XamlKey = new("\\{loc:T '([^']*)'\\}", RegexOptions.Compiled);

		private static String FindSourceFolder()
		{
			DirectoryInfo? directory = new(AppContext.BaseDirectory);
			while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SFallConfigurator.slnx")))
			{
				directory = directory.Parent;
			}

			Assert.True(directory is not null, "The source files of the program were not found above " + AppContext.BaseDirectory);
			return Path.Combine(directory!.FullName, "SFallConfigurator");
		}

		private static IEnumerable<String> SourceFiles(String pattern)
		{
			String folder = FindSourceFolder();
			return Directory.EnumerateFiles(folder, pattern, SearchOption.AllDirectories)
				.Where(file => !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar));
		}

		private static String Unescape(String literal)
		{
			return literal.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\'", "'").Replace("\\t", "\t").Replace("\\\\", "\\");
		}

		private static String ReadCode(String file)
		{
			return JoinedLiterals.Replace(File.ReadAllText(file), String.Empty);
		}

		private static Dictionary<String, String> ReadDictionary(String language)
		{
			using Stream stream = typeof(Localizer).Assembly.GetManifestResourceStream("SFallConfigurator.Localization." + language + ".json")!;
			return JsonSerializer.Deserialize<Dictionary<String, String>>(stream)!;
		}

		/// <summary>
		/// The texts the sources hand to the translation: Localizer.T and F calls, XAML, dialog messages and captions, file types.
		/// </summary>
		private static SortedSet<String> FindTranslatedTexts()
		{
			SortedSet<String> texts = [];
			foreach (String file in SourceFiles("*.cs"))
			{
				String code = ReadCode(file);
				foreach (Match match in LocalizedCall.Matches(code))
				{
					texts.Add(Unescape(match.Groups[1].Value));
				}

				foreach (Match match in InteractionCall.Matches(code))
				{
					texts.Add(Unescape(match.Groups[1].Value));
					if (match.Groups[2].Success)
					{
						texts.Add(Unescape(match.Groups[2].Value));
					}
				}

				foreach (Match match in InteractionCaption.Matches(code))
				{
					texts.Add(Unescape(match.Groups[1].Value));
				}

				foreach (Match match in FilterLiteral.Matches(code))
				{
					texts.Add(Unescape(match.Groups[1].Value));
				}
			}

			foreach (String file in SourceFiles("*.xaml"))
			{
				foreach (Match match in XamlKey.Matches(File.ReadAllText(file)))
				{
					texts.Add(System.Net.WebUtility.HtmlDecode(match.Groups[1].Value));
				}
			}

			return texts;
		}

		[Fact]
		public void EveryTextOfTheSourcesIsTranslatedInEveryLanguage()
		{
			SortedSet<String> texts = FindTranslatedTexts();
			Assert.True(texts.Count > 60, "Too few texts were found in the sources: " + texts.Count);

			List<String> missing = [];
			foreach (LanguageInfo language in Localizer.Languages.Where(language => language.Code != "en"))
			{
				Dictionary<String, String> dictionary = ReadDictionary(language.Code);
				missing.AddRange(texts.Where(text => !dictionary.ContainsKey(text)).Select(text => language.Code + ": " + text));
			}

			Assert.True(missing.Count == 0, String.Join(Environment.NewLine, missing));
		}

		[Fact]
		public void TheDictionariesKeepNoTextThatNothingUses()
		{
			HashSet<String> literals = [.. BuiltAtRunTime];
			foreach (String file in SourceFiles("*.cs"))
			{
				foreach (Match match in StringLiteral.Matches(ReadCode(file)))
				{
					String text = Unescape(match.Groups[1].Value);
					literals.Add(text);
					foreach (String part in text.Split('|'))
					{
						literals.Add(part);
					}
				}
			}

			foreach (String file in SourceFiles("*.xaml"))
			{
				foreach (Match match in XamlKey.Matches(File.ReadAllText(file)))
				{
					literals.Add(System.Net.WebUtility.HtmlDecode(match.Groups[1].Value));
				}
			}

			List<String> unused = ReadDictionary("fr").Keys.Where(key => !literals.Contains(key)).ToList();

			Assert.True(unused.Count == 0, "Not used anywhere: " + String.Join(" | ", unused));
		}

		[Fact]
		public void EveryTranslationKeepsTheShapeOfTheText()
		{
			Dictionary<String, String> french = ReadDictionary("fr");
			List<String> problems = [];
			foreach (LanguageInfo language in Localizer.Languages.Where(language => language.Code != "en"))
			{
				Dictionary<String, String> dictionary = ReadDictionary(language.Code);
				problems.AddRange(french.Keys.Except(dictionary.Keys).Select(key => language.Code + ": missing " + key));
				problems.AddRange(dictionary.Keys.Except(french.Keys).Select(key => language.Code + ": unknown " + key));

				foreach ((String key, String value) in dictionary)
				{
					if (value.Length == 0 || value.Trim().Length == 0)
					{
						problems.Add(language.Code + ": empty " + key);
						continue;
					}

					Boolean sameStart = Char.IsWhiteSpace(key[0]) == Char.IsWhiteSpace(value[0]);
					Boolean sameEnd = Char.IsWhiteSpace(key[^1]) == Char.IsWhiteSpace(value[^1]);
					Boolean sameNewLines = key.Count(character => character == '\n') == value.Count(character => character == '\n');
					Boolean samePlaceholders = Enumerable.Range(0, 4).All(index => key.Contains("{" + index + "}") == value.Contains("{" + index + "}"));
					Boolean sameColon = key.TrimEnd().EndsWith(':') == value.TrimEnd().EndsWith(':');
					if (!sameStart || !sameEnd || !sameNewLines || !samePlaceholders || !sameColon)
					{
						problems.Add(language.Code + ": " + key);
					}
				}
			}

			Assert.True(problems.Count == 0, String.Join(Environment.NewLine, problems));
		}
	}
}