namespace SFallConfigurator.Tests
{
	using System;
	using System.IO;
	using System.Linq;
	using Configuration;
	using Infrastructure;
	using Options;
	using ViewModels;
	using Xunit;

	/// <summary>
	/// The backups of the game files in the folder old_ini.
	/// </summary>
	public sealed class BackupTests
	{
		private static TestFolder CreateGame()
		{
			TestFolder folder = new();
			folder.Write("ddraw.ini", "[Main]\r\nHiResMode=1\r\n[Misc]\r\nExtraSaveSlots=0\r\n");
			folder.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\n");
			return folder;
		}

		private static MainViewModel Open(TestFolder folder, TestInteraction interaction, TestHost? host = null)
		{
			GameConfiguration configuration = new(folder.Path);
			configuration.Load();
			MainViewModel viewModel = new(configuration, new AppServices(interaction), host ?? new TestHost());
			viewModel.Load();
			return viewModel;
		}

		[Fact]
		public void SavingKeepsThePreviousVersionOfTheFileInOldIni()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder, new TestInteraction());
			viewModel.AllOptions.OfType<BoolOption>().First(option => option.Id == "extraSaveSlots").Value = true;

			Assert.True(viewModel.ConfirmClose());

			BackupSet set = Assert.Single(viewModel.Configuration.Backups.GetSets());
			Assert.Equal(["ddraw.ini"], set.Files);
			Assert.Contains("ExtraSaveSlots=0", File.ReadAllText(Path.Combine(set.Folder, "ddraw.ini")));
			Assert.Contains("ExtraSaveSlots=1", File.ReadAllText(folder.Combine("ddraw.ini")));
			Assert.StartsWith(folder.Combine("old_ini"), set.Folder, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void NothingIsCopiedWhenNothingChanged()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder, new TestInteraction());

			Assert.True(viewModel.ConfirmClose());

			Assert.False(Directory.Exists(folder.Combine("old_ini")));
		}

		[Fact]
		public void OnlyTheFilesThatChangeAreCopied()
		{
			using TestFolder folder = CreateGame();
			BackupStore store = new(folder.Path);

			BackupSet? set = store.Backup([folder.Combine("ddraw.ini"), folder.Combine("missing.ini"), Path.Combine(Path.GetTempPath(), "outside.ini")]);

			Assert.NotNull(set);
			Assert.Equal(["ddraw.ini"], set.Files);
			Assert.Null(store.Backup([folder.Combine("missing.ini")]));
		}

		[Fact]
		public void TwoSavesInTheSameSecondAreTwoBackups()
		{
			using TestFolder folder = CreateGame();
			DateTime now = new(2026, 10, 12, 15, 30, 12);
			BackupStore store = new(folder.Path, () => now);

			store.Backup([folder.Combine("ddraw.ini")]);
			store.Backup([folder.Combine("ddraw.ini")]);

			Assert.Equal(2, store.GetSets().Count);
			Assert.Equal(now, store.GetSets()[^1].Time);
		}

		[Fact]
		public void TheNewestBackupsComeFirstAndTheOldestAreDeleted()
		{
			using TestFolder folder = CreateGame();
			DateTime now = new(2026, 1, 1, 0, 0, 0);
			BackupStore store = new(folder.Path, () => now);

			for (Int32 index = 0; index < BackupStore.KeptSets + 5; index++)
			{
				now = now.AddMinutes(1);
				store.Backup([folder.Combine("ddraw.ini")]);
			}

			System.Collections.Generic.IReadOnlyList<BackupSet> sets = store.GetSets();
			Assert.Equal(BackupStore.KeptSets, sets.Count);
			Assert.Equal(now, sets[0].Time);
			Assert.True(sets[0].Time > sets[^1].Time);
		}

		[Fact]
		public void ABackupCanBeRestored()
		{
			using TestFolder folder = CreateGame();
			BackupStore store = new(folder.Path);
			BackupSet set = store.Backup([folder.Combine("ddraw.ini")])!;
			folder.Write("ddraw.ini", "[Misc]\r\nExtraSaveSlots=1\r\n");

			store.Restore(set);

			Assert.Contains("ExtraSaveSlots=0", File.ReadAllText(folder.Combine("ddraw.ini")));
		}

		[Fact]
		public void RestoringKeepsTheCurrentFilesInANewBackupAndReloadsTheFolder()
		{
			using TestFolder folder = CreateGame();
			TestInteraction interaction = new();
			TestHost host = new();
			MainViewModel first = Open(folder, interaction, host);
			first.AllOptions.OfType<BoolOption>().First(option => option.Id == "extraSaveSlots").Value = true;
			first.ConfirmClose();
			interaction.ItemToPick = 0;

			first.RestoreBackupCommand.Execute(null);

			Assert.Contains("ExtraSaveSlots=0", File.ReadAllText(folder.Combine("ddraw.ini")));
			Assert.Equal(1, host.ReloadCount);
			Assert.Equal(2, first.Configuration.Backups.GetSets().Count);
			Assert.Single(interaction.PickedFrom);
		}

		[Fact]
		public void CancellingTheChoiceRestoresNothing()
		{
			using TestFolder folder = CreateGame();
			TestInteraction interaction = new();
			TestHost host = new();
			MainViewModel viewModel = Open(folder, interaction, host);
			viewModel.AllOptions.OfType<BoolOption>().First(option => option.Id == "extraSaveSlots").Value = true;
			viewModel.ConfirmClose();

			viewModel.RestoreBackupCommand.Execute(null);

			Assert.Contains("ExtraSaveSlots=1", File.ReadAllText(folder.Combine("ddraw.ini")));
			Assert.Equal(0, host.ReloadCount);
		}

		[Fact]
		public void WithoutBackupTheUserIsToldHowTheyAreMade()
		{
			using TestFolder folder = CreateGame();
			TestInteraction interaction = new();
			MainViewModel viewModel = Open(folder, interaction);

			viewModel.RestoreBackupCommand.Execute(null);

			Assert.Contains("old_ini", Assert.Single(interaction.Messages));
		}
	}
}