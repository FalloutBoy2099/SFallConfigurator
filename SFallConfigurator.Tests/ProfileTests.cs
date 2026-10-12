namespace SFallConfigurator.Tests
{
	using System;
	using System.IO;
	using System.Linq;
	using Configuration;
	using Infrastructure;
	using Options;
	using Services;
	using ViewModels;
	using Xunit;

	/// <summary>
	/// The profiles: sets of option values that the user keeps and applies again.
	/// </summary>
	public sealed class ProfileTests : IDisposable
	{
		private readonly String _folder = Path.Combine(Path.GetTempPath(), "SFallConfiguratorTests", "profiles-" + Guid.NewGuid().ToString("N"));

		public ProfileTests()
		{
			OptionHistory.CoalesceMilliseconds = -1;
		}

		public void Dispose()
		{
			OptionHistory.CoalesceMilliseconds = 600;
			if (Directory.Exists(_folder))
			{
				Directory.Delete(_folder, recursive: true);
			}
		}

		private static MainViewModel Open(TestFolder folder, TestInteraction interaction)
		{
			GameConfiguration configuration = new(folder.Path);
			configuration.Load();
			MainViewModel viewModel = new(configuration, new AppServices(interaction), new TestHost());
			viewModel.Load();
			return viewModel;
		}

		private static TestFolder CreateGame(String slots, String quickSave)
		{
			TestFolder folder = new();
			folder.Write("ddraw.ini", "[Main]\r\nHiResMode=1\r\n[Misc]\r\nExtraSaveSlots=" + slots + "\r\nAutoQuickSave=" + quickSave + "\r\n");
			folder.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\nmaster_dat=master.dat\r\n");
			return folder;
		}

		private static T Find<T>(MainViewModel viewModel, String id)
			where T : ConfigOption
		{
			return (T)viewModel.AllOptions.First(option => option.Id == id);
		}

		[Fact]
		public void AProfileKeepsTheValuesAndTheNamesAreListed()
		{
			ProfileStore store = new(_folder);

			store.Save(new Profile("Mine", new System.Collections.Generic.Dictionary<String, String> { ["sfall||ExtraSaveSlots"] = "1" }));
			store.Save(new Profile("alpha", new System.Collections.Generic.Dictionary<String, String>()));

			Assert.Equal(["alpha", "Mine"], store.GetNames());
			Assert.Equal("1", store.Load("Mine")!.Values["sfall||ExtraSaveSlots"]);
			Assert.True(store.Exists("Mine"));
			store.Delete("Mine");
			Assert.False(store.Exists("Mine"));
			Assert.Null(store.Load("Mine"));
		}

		[Fact]
		public void ANameCannotHoldACharacterThatAFileNameForbids()
		{
			Assert.DoesNotContain('/', ProfileStore.CleanName("a/b:c"));
			Assert.Equal("name", ProfileStore.CleanName("  name. "));
		}

		[Fact]
		public void AProfileSavedHereCanBeAppliedToAnotherGame()
		{
			TestInteraction interaction = new() { PromptAnswer = "My settings" };
			using TestFolder source = CreateGame("1", "5");
			using TestFolder target = CreateGame("0", "0");
			ProfileStore store = new(_folder);
			MainViewModel first = Open(source, interaction);
			ProfilesViewModel profiles = new(store, () => first.AllOptions, interaction);

			profiles.SaveCommand.Execute(null);
			MainViewModel second = Open(target, interaction);
			ProfilesViewModel others = new(store, () => second.AllOptions, interaction);

			Assert.True(others.HasProfiles);
			Assert.Equal("My settings", others.ApplyChoices.Single().Text);
			Assert.Equal(2, others.Apply("My settings"));

			Assert.True(Find<BoolOption>(second, "extraSaveSlots").Value);
			Assert.Equal(5, Find<NumberOption>(second, "quickSavePages").Value);
			Assert.Equal("1", second.Configuration.Ddraw.GetValue("Misc", "ExtraSaveSlots"));
			Assert.True(second.HasPendingChanges);
		}

		[Fact]
		public void AppliedValuesCanBeUndoneOneByOne()
		{
			TestInteraction interaction = new() { PromptAnswer = "p" };
			using TestFolder source = CreateGame("1", "5");
			using TestFolder target = CreateGame("0", "0");
			ProfileStore store = new(_folder);
			MainViewModel first = Open(source, interaction);
			new ProfilesViewModel(store, () => first.AllOptions, interaction).SaveCommand.Execute(null);
			MainViewModel second = Open(target, interaction);
			ProfilesViewModel profiles = new(store, () => second.AllOptions, interaction);

			profiles.Apply("p");
			Find<BoolOption>(second, "extraSaveSlots").Undo();

			Assert.False(Find<BoolOption>(second, "extraSaveSlots").Value);
			Assert.Equal(5, Find<NumberOption>(second, "quickSavePages").Value);
		}

		[Fact]
		public void ApplyingAProfileThatChangesNothingSaysSo()
		{
			TestInteraction interaction = new() { PromptAnswer = "same" };
			using TestFolder game = CreateGame("1", "5");
			MainViewModel viewModel = Open(game, interaction);
			ProfilesViewModel profiles = new(new ProfileStore(_folder), () => viewModel.AllOptions, interaction);
			profiles.SaveCommand.Execute(null);
			interaction.Messages.Clear();

			Assert.Equal(0, profiles.Apply("same"));

			Assert.Single(interaction.Messages);
			Assert.False(viewModel.HasPendingChanges);
		}

		[Fact]
		public void AProfileDoesNotCarryThePathsOfTheGame()
		{
			TestInteraction interaction = new() { PromptAnswer = "p" };
			using TestFolder game = CreateGame("1", "5");
			MainViewModel viewModel = Open(game, interaction);
			ProfilesViewModel profiles = new(new ProfileStore(_folder), () => viewModel.AllOptions, interaction);

			Profile profile = profiles.Capture("p");

			Assert.DoesNotContain(profile.Values.Keys, key => key.Contains("master_dat", StringComparison.OrdinalIgnoreCase));
			Assert.DoesNotContain(profile.Values.Keys, key => key.Contains("language", StringComparison.OrdinalIgnoreCase));
			Assert.Contains("sfall||ExtraSaveSlots", profile.Values.Keys, StringComparer.OrdinalIgnoreCase);
		}

		[Fact]
		public void ANameThatExistsIsReplacedOnlyWhenTheUserAgrees()
		{
			TestInteraction interaction = new() { PromptAnswer = "p", ConfirmAnswer = false };
			using TestFolder game = CreateGame("1", "5");
			MainViewModel viewModel = Open(game, interaction);
			ProfileStore store = new(_folder);
			ProfilesViewModel profiles = new(store, () => viewModel.AllOptions, interaction);
			profiles.SaveCommand.Execute(null);
			Find<BoolOption>(viewModel, "extraSaveSlots").Value = false;

			profiles.SaveCommand.Execute(null);
			Assert.Equal("1", store.Load("p")!.Values["sfall||ExtraSaveSlots"]);

			interaction.ConfirmAnswer = true;
			profiles.SaveCommand.Execute(null);
			Assert.Equal("0", store.Load("p")!.Values["sfall||ExtraSaveSlots"]);
		}

		[Fact]
		public void AProfileCanBeDeleted()
		{
			TestInteraction interaction = new() { PromptAnswer = "p" };
			using TestFolder game = CreateGame("1", "5");
			MainViewModel viewModel = Open(game, interaction);
			ProfileStore store = new(_folder);
			ProfilesViewModel profiles = new(store, () => viewModel.AllOptions, interaction);
			profiles.SaveCommand.Execute(null);

			profiles.DeleteChoices.Single().ChooseCommand.Execute(null);

			Assert.False(profiles.HasProfiles);
			Assert.Empty(store.GetNames());
		}
	}
}