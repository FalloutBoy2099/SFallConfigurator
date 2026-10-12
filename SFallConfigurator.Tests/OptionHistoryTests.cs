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
	/// The history of an option (undo, redo), its default value and the value that was saved.
	/// </summary>
	public sealed class OptionHistoryTests : IDisposable
	{
		public OptionHistoryTests()
		{
			// Every change is a step of its own.
			OptionHistory.CoalesceMilliseconds = -1;
		}

		public void Dispose()
		{
			OptionHistory.CoalesceMilliseconds = 600;
			OptionHistory.Clock = () => Environment.TickCount64;
		}

		private static MainViewModel Open(TestFolder folder)
		{
			GameConfiguration configuration = new(folder.Path);
			configuration.Load();
			MainViewModel viewModel = new(configuration, new AppServices(new TestInteraction()), new TestHost());
			viewModel.Load();
			return viewModel;
		}

		private static TestFolder CreateGame()
		{
			TestFolder folder = new();
			folder.Write("ddraw.ini", "[Main]\r\nHiResMode=1\r\n[Misc]\r\nExtraSaveSlots=1\r\nSpeedMultiInitial=150\r\nSaveInCombatFix=0\r\nReloadWeaponKey=17\r\n[Speed]\r\nEnable=0\r\n");
			folder.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\n");
			return folder;
		}

		private static T Find<T>(MainViewModel viewModel, String id)
			where T : ConfigOption
		{
			return (T)viewModel.Sfall.Pages.SelectMany(page => page.Options).First(option => option.Id == id);
		}

		[Fact]
		public void AChangeCanBeUndoneAndDoneAgain()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);
			NumberOption speed = Find<NumberOption>(viewModel, "gameSpeed");

			speed.Value = 200;
			speed.Value = 300;
			Assert.True(speed.CanUndo);
			Assert.False(speed.CanRedo);

			speed.Undo();
			Assert.Equal(200, speed.Value);
			Assert.Equal("200", viewModel.Configuration.Ddraw.GetValue(null, "SpeedMultiInitial"));
			Assert.True(speed.CanRedo);

			speed.Undo();
			Assert.Equal(150, speed.Value);
			Assert.False(speed.CanUndo);
			Assert.False(speed.IsModified);

			speed.Redo();
			speed.Redo();
			Assert.Equal(300, speed.Value);
			Assert.Equal("300", viewModel.Configuration.Ddraw.GetValue(null, "SpeedMultiInitial"));
		}

		[Fact]
		public void ANewChangeForgetsWhatWasUndone()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);
			NumberOption speed = Find<NumberOption>(viewModel, "gameSpeed");

			speed.Value = 200;
			speed.Undo();
			speed.Value = 250;

			Assert.False(speed.CanRedo);
		}

		[Fact]
		public void ChangesThatFollowEachOtherQuicklyAreOneStep()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);
			NumberOption speed = Find<NumberOption>(viewModel, "gameSpeed");
			Int64 now = 1_000;
			OptionHistory.Clock = () => now;
			OptionHistory.CoalesceMilliseconds = 600;

			speed.Value = 160;
			now += 100;
			speed.Value = 170;
			now += 100;
			speed.Value = 180;
			now += 5_000;
			speed.Value = 400;

			speed.Undo();
			Assert.Equal(180, speed.Value);
			speed.Undo();
			Assert.Equal(150, speed.Value);
			Assert.False(speed.CanUndo);
		}

		[Fact]
		public void UndoWorksForEveryKindOfOption()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);
			BoolOption slots = Find<BoolOption>(viewModel, "extraSaveSlots");
			ChoiceOption saving = Find<ChoiceOption>(viewModel, "saveInCombat");
			KeyOption reload = Find<KeyOption>(viewModel, "reloadWeaponKey");

			slots.Value = false;
			saving.SelectedIndex = 2;
			reload.IsChecked = false;

			slots.Undo();
			saving.Undo();
			reload.Undo();

			Assert.True(slots.Value);
			Assert.Equal(0, saving.SelectedIndex);
			Assert.True(reload.IsChecked);
			Assert.Equal("1", viewModel.Configuration.Ddraw.GetValue("Misc", "ExtraSaveSlots"));
			Assert.Equal("0", viewModel.Configuration.Ddraw.GetValue("Misc", "SaveInCombatFix"));
			Assert.Equal("17", viewModel.Configuration.Ddraw.GetValue("Misc", "ReloadWeaponKey"));
		}

		[Fact]
		public void TheDefaultValueIsShownAndCanBeSet()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);
			NumberOption speed = Find<NumberOption>(viewModel, "gameSpeed");

			Assert.Equal("100", speed.DefaultText);
			Assert.False(speed.IsDefault);
			Assert.Contains("100", speed.HelpText);

			speed.ResetCommand.Execute(null);

			Assert.Equal(100, speed.Value);
			Assert.True(speed.IsDefault);
			Assert.False(speed.CanResetToDefault);
			Assert.Equal("100", viewModel.Configuration.Ddraw.GetValue(null, "SpeedMultiInitial"));

			speed.Undo();
			Assert.Equal(150, speed.Value);
		}

		[Fact]
		public void AChoiceThatSaysDefaultHasThatDefault()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);
			ChoiceOption saving = Find<ChoiceOption>(viewModel, "saveInCombat");

			Assert.Equal("0", saving.KnownDefault);
			Assert.True(saving.IsDefault);
			saving.SelectedIndex = 1;
			Assert.False(saving.IsDefault);
		}

		[Fact]
		public void TheSavedValueCanBePutBack()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);
			NumberOption speed = Find<NumberOption>(viewModel, "gameSpeed");

			Assert.False(speed.CanRevert);
			speed.Value = 800;
			speed.Value = 900;
			Assert.True(speed.CanRevert);

			speed.RevertCommand.Execute(null);

			Assert.Equal(150, speed.Value);
			Assert.False(speed.IsModified);
			speed.Undo();
			Assert.Equal(900, speed.Value);
		}

		[Fact]
		public void TheHelpTellsWhereTheOptionIsStored()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);
			BoolOption slots = Find<BoolOption>(viewModel, "extraSaveSlots");

			Assert.Contains("ddraw.ini", slots.HelpText);
			Assert.Contains("ExtraSaveSlots", slots.HelpText);
		}

		[Fact]
		public void AnUnavailableOptionHasNothingToUndo()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);
			BoolOption unknown = Find<BoolOption>(viewModel, "smartBehavior");

			Assert.False(unknown.IsAvailable);
			Assert.False(unknown.CanUndo);
			Assert.False(unknown.CanRevert);
			Assert.False(unknown.CanResetToDefault);
		}
	}
}