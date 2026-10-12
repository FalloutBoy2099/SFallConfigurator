namespace SFallConfigurator.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Configuration;
	using Infrastructure;
	using Options;
	using ViewModels;
	using Xunit;

	/// <summary>
	/// Checks against a real installation. Set SF_CONFIGURATOR_TEST_GAME to a game folder (only its
	/// configuration files are used, from a temporary copy); without it the tests do nothing.
	/// </summary>
	public class RealGameTests
	{
		private static TestFolder? CopyGame()
		{
			String? source = Environment.GetEnvironmentVariable("SF_CONFIGURATOR_TEST_GAME");
			if (String.IsNullOrEmpty(source) || !Directory.Exists(source))
			{
				return null;
			}

			TestFolder folder = new();
			foreach (String file in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
			{
				FileInfo info = new(file);
				if (info.Length > 2_000_000)
				{
					continue;
				}

				String target = folder.Combine(Path.GetRelativePath(source, file));
				Directory.CreateDirectory(Path.GetDirectoryName(target)!);
				File.Copy(file, target);
			}

			return folder;
		}

		private static IEnumerable<ConfigOption> AllOptions(MainViewModel viewModel)
		{
			return viewModel.Sfall.Pages.Concat(viewModel.HiRes.Pages)
				.SelectMany(page => page.Groups)
				.SelectMany(group => group.Items)
				.OfType<ConfigOption>();
		}

		private static MainViewModel Open(String path, TestInteraction interaction)
		{
			GameConfiguration configuration = new(path);
			configuration.Load();
			MainViewModel viewModel = new(configuration, new AppServices(interaction), new TestHost());
			viewModel.Load();
			return viewModel;
		}

		[RealDataFact("SF_CONFIGURATOR_TEST_GAME")]
		public void OpeningAGameChangesNothing()
		{
			using TestFolder? folder = CopyGame();
			if (folder is null)
			{
				return;
			}

			TestInteraction interaction = new();
			MainViewModel viewModel = Open(folder.Path, interaction);

			Assert.True(viewModel.IsGameConfigured);
			Assert.True(AllOptions(viewModel).Count(option => option.IsAvailable) > 60);
			Assert.All(AllOptions(viewModel), option => Assert.False(option.IsModified, option.Key));
			Assert.True(viewModel.ConfirmClose());
			Assert.Equal(0, interaction.Questions);
		}

		[RealDataFact("SF_CONFIGURATOR_TEST_GAME")]
		public void SwitchingEveryCheckBoxTwiceRestoresTheFiles()
		{
			using TestFolder? folder = CopyGame();
			if (folder is null)
			{
				return;
			}

			MainViewModel viewModel = Open(folder.Path, new TestInteraction());
			GameConfiguration configuration = viewModel.Configuration;

			String[] before = Snapshot(configuration);
			Int32 switched = 0;
			// Options with their own value meaning (such as 2 for "on") may normalize an unexpected value, so they are left out.
			foreach (BoolOption option in AllOptions(viewModel).OfType<BoolOption>().Where(option => option.IsEnabled && option.Key != "AutoSave" && option.Interpret is null && option.TrueValue == "1" && option.FalseValue == "0"))
			{
				Boolean? original = option.Value;
				option.Value = original == true ? false : true;
				Assert.True(option.IsModified, option.Key);
				option.Value = original;
				Assert.False(option.IsModified, option.Key);
				switched++;
			}

			Assert.True(switched > 30);
			Assert.Equal(before, Snapshot(configuration));
		}

		[RealDataFact("SF_CONFIGURATOR_TEST_GAME")]
		public void ChangesAreWrittenToTheRightFilesAndSurviveAReload()
		{
			using TestFolder? folder = CopyGame();
			if (folder is null)
			{
				return;
			}

			MainViewModel viewModel = Open(folder.Path, new TestInteraction());
			BoolOption npcAutoLevel = AllOptions(viewModel).OfType<BoolOption>().First(option => option.Key == "NPCAutoLevel");
			NumberOption timeLimit = AllOptions(viewModel).OfType<NumberOption>().First(option => option.Key == "TimeLimit");
			Boolean? level = npcAutoLevel.Value;
			Int32 limit = timeLimit.Value;

			npcAutoLevel.Value = !level;
			timeLimit.Value = limit == 5 ? 6 : 5;
			viewModel.Configuration.Save();

			MainViewModel reloaded = Open(folder.Path, new TestInteraction());
			Assert.Equal(!level, AllOptions(reloaded).OfType<BoolOption>().First(option => option.Key == "NPCAutoLevel").Value);
			Assert.Equal(limit == 5 ? 6 : 5, AllOptions(reloaded).OfType<NumberOption>().First(option => option.Key == "TimeLimit").Value);
		}

		private static String[] Snapshot(GameConfiguration configuration)
		{
			return new[] { configuration.Ddraw, configuration.Sfall, configuration.HiRes, configuration.SfallMods, configuration.GameCfg, configuration.Translations }
				.Where(document => document is not null)
				.SelectMany(document => document!.Lines)
				.ToArray();
		}
	}
}