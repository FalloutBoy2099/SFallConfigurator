namespace SFallConfigurator.Tests
{
	using System;
	using System.Linq;
	using Configuration;
	using Infrastructure;
	using Options;
	using ViewModels;
	using Xunit;

	/// <summary>
	/// Adding to the file a key that it lacks.
	/// </summary>
	public sealed class AddKeyTests
	{
		private static MainViewModel Open(TestFolder folder)
		{
			GameConfiguration configuration = new(folder.Path);
			configuration.Load();
			MainViewModel viewModel = new(configuration, new AppServices(new TestInteraction()), new TestHost());
			viewModel.Load();
			return viewModel;
		}

		private static TestFolder CreateGame(String ddraw)
		{
			TestFolder folder = new();
			folder.Write("ddraw.ini", ddraw);
			folder.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\n");
			return folder;
		}

		private static T Find<T>(MainViewModel viewModel, String id)
			where T : ConfigOption
		{
			return (T)viewModel.AllOptions.First(option => option.Id == id);
		}

		[Fact]
		public void AMissingKeyIsAddedToItsSection()
		{
			using TestFolder folder = CreateGame("[Main]\r\nHiResMode=1\r\n[CombatAI]\r\nTryToFindTargets=1\r\n");
			MainViewModel viewModel = Open(folder);
			ChoiceOption burst = Find<ChoiceOption>(viewModel, "burstFriendlyFire");

			Assert.False(burst.IsEnabled);
			Assert.True(burst.CanAddKey);
			Assert.Contains("right-click", burst.HelpText);

			burst.AddKeyCommand.Execute(null);

			Assert.True(burst.IsEnabled);
			Assert.False(burst.CanAddKey);
			Assert.Equal("0", viewModel.Configuration.Ddraw.GetValue("CombatAI", "CheckBurstFriendlyFire"));
			Assert.True(viewModel.HasPendingChanges);
			burst.SelectedIndex = 2;
			Assert.Equal("2", viewModel.Configuration.Ddraw.GetValue("CombatAI", "CheckBurstFriendlyFire"));
		}

		[Fact]
		public void TheSectionIsCreatedWhenTheFileLacksIt()
		{
			using TestFolder folder = CreateGame("[Main]\r\nHiResMode=1\r\n");
			MainViewModel viewModel = Open(folder);

			Find<BoolOption>(viewModel, "smartBehavior").AddKey();

			Assert.Equal("0", viewModel.Configuration.Ddraw.GetValue("CombatAI", "SmartBehavior"));
		}

		[Fact]
		public void ThePlaceOfTheKeyFollowsTheLayoutOfTheFile()
		{
			using TestFolder oldLayout = CreateGame("[Main]\r\n[Misc]\r\nSingleCore=1\r\n");
			using TestFolder newLayout = CreateGame("[Main]\r\n[System]\r\nSkipOpeningMovies=0\r\n");
			MainViewModel old = Open(oldLayout);
			MainViewModel recent = Open(newLayout);

			Find<BoolOption>(old, "processorIdle").AddKey();
			Find<BoolOption>(recent, "processorIdle").AddKey();

			Assert.NotNull(old.Configuration.Ddraw.GetValue("Misc", "ProcessorIdle"));
			Assert.NotNull(recent.Configuration.Ddraw.GetValue("System", "ProcessorIdle"));
		}

		[Fact]
		public void TheAddedKeyHasTheDefaultValue()
		{
			using TestFolder folder = CreateGame("[Main]\r\n[Misc]\r\nExtraSaveSlots=0\r\n");
			MainViewModel viewModel = Open(folder);
			NumberOption speed = Find<NumberOption>(viewModel, "reloadReserve");

			speed.AddKey();

			Assert.Equal("-1", viewModel.Configuration.Ddraw.GetValue("Misc", "ReloadReserve"));
			Assert.Equal(-1, speed.Value);
		}

		[Fact]
		public void AKeyThatTheFileHasCannotBeAddedAgain()
		{
			using TestFolder folder = CreateGame("[Main]\r\n[Misc]\r\nExtraSaveSlots=0\r\n");
			MainViewModel viewModel = Open(folder);

			Assert.False(Find<BoolOption>(viewModel, "extraSaveSlots").CanAddKey);
		}

		[Fact]
		public void AnOptionWithoutKnownSectionCannotBeAdded()
		{
			using TestFolder folder = CreateGame("[Main]\r\n");
			MainViewModel viewModel = Open(folder);
			ConfigOption option = viewModel.AllOptions.First(candidate => candidate.Key == "QuestsScrollButtonsX");

			Assert.False(option.CanAddKey);
		}

		[Fact]
		public void AFileThatDoesNotExistGetsNoKey()
		{
			using TestFolder folder = CreateGame("[Main]\r\n");
			MainViewModel viewModel = Open(folder);

			Assert.False(viewModel.AllOptions.First(option => option.Key == "SCR_WIDTH").CanAddKey);
		}

		[Fact]
		public void EverySectionOfTheCatalogIsAName()
		{
			foreach (CatalogPage page in OptionCatalog.Pages.Values)
			{
				foreach (CatalogItem item in page.Groups.SelectMany(group => group.Items).Where(item => item.AddToSection is not null))
				{
					Assert.All(item.AddToSection!.Split('|'), name => Assert.Matches(@"^[A-Za-z][A-Za-z0-9]*$", name));
				}
			}
		}
	}
}