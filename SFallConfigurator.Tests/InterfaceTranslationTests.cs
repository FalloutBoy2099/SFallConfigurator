namespace SFallConfigurator.Tests
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using Configuration;
	using Infrastructure;
	using Localization;
	using ViewModels;
	using Xunit;

	/// <summary>
	/// The whole interface must be translated: every text the view models offer has to be in the dictionary of each language.
	/// </summary>
	public sealed class InterfaceTranslationTests : IDisposable
	{
		private readonly TestFolder _settings = new();

		public InterfaceTranslationTests()
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
		public void EveryTextOfTheInterfaceIsInTheDictionaryOfEveryLanguage()
		{
			using TestFolder game = new();
			game.Write("ddraw.ini", "[Main]\r\nExtraCRC=0x1\r\n");
			game.Write("f2_res.ini", "[MAIN]\r\nSCR_WIDTH=800\r\n");
			game.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\nmaster_patches=data\r\n");
			game.Write("sfall/sfall-mods.ini", "[Misc]\r\nAutoSave=0\r\n");

			List<String> problems = [];
			foreach (LanguageInfo language in Localizer.Languages.Where(language => language.Code != "en"))
			{
				Localizer.Instance.Apply(language.Code);
				Localizer.ClearUntranslated();

				GameConfiguration configuration = new(game.Path);
				configuration.Load();
				MainViewModel viewModel = new(configuration, new TestInteraction(), () => { }, _ => { }, _ => { });
				viewModel.Load();
				Visit(viewModel, new HashSet<Object>(ReferenceEqualityComparer.Instance), 0);

				problems.AddRange(Localizer.GetUntranslated().Select(text => language.Code + ": " + text));
			}

			Assert.True(problems.Count == 0, String.Join(Environment.NewLine, problems.Distinct()));
		}

		[Fact]
		public void EveryKeyNameIsTranslated()
		{
			foreach (LanguageInfo language in Localizer.Languages.Where(language => language.Code != "en"))
			{
				Localizer.Instance.Apply(language.Code);
				Localizer.ClearUntranslated();

				foreach (DirectInputKeys.KeyInfo key in DirectInputKeys.All)
				{
					DirectInputKeys.GetDisplayName(key.Code, allowModifiers: true);
				}

				foreach (Int32 code in new[] { 0, DirectInputKeys.ModifierControl, DirectInputKeys.ModifierAlt, DirectInputKeys.ModifierShift })
				{
					DirectInputKeys.GetDisplayName(code, allowModifiers: true);
				}

				Assert.True(Localizer.GetUntranslated().Count == 0, language.Code + ": " + String.Join(", ", Localizer.GetUntranslated()));
			}
		}

		/// <summary>
		/// Reads every property of the view model tree, so that every text goes through the translation.
		/// </summary>
		private static void Visit(Object? node, HashSet<Object> visited, Int32 depth)
		{
			if (node is null || depth > 12 || node is String || node.GetType().IsPrimitive || node.GetType().IsEnum || !visited.Add(node))
			{
				return;
			}

			if (node is IEnumerable sequence)
			{
				foreach (Object? item in sequence)
				{
					Visit(item, visited, depth + 1);
				}

				return;
			}

			if (node.GetType().Namespace?.StartsWith("SFallConfigurator", StringComparison.Ordinal) != true)
			{
				return;
			}

			foreach (PropertyInfo property in node.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
			{
				if (property.GetIndexParameters().Length > 0 || !property.CanRead)
				{
					continue;
				}

				Object? value;
				try
				{
					value = property.GetValue(node);
				}
				catch (TargetInvocationException)
				{
					continue;
				}

				Visit(value, visited, depth + 1);
			}
		}
	}
}