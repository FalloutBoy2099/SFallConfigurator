namespace SFallConfigurator.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Configuration;
	using Infrastructure;
	using Options;
	using ViewModels;
	using Xunit;

	/// <summary>
	/// The preview of the changes that saving would write.
	/// </summary>
	public sealed class ChangePreviewTests
	{
		private static MainViewModel Open(TestFolder folder, TestInteraction interaction)
		{
			GameConfiguration configuration = new(folder.Path);
			configuration.Load();
			MainViewModel viewModel = new(configuration, new AppServices(interaction), new TestHost());
			viewModel.Load();
			return viewModel;
		}

		private static TestFolder CreateGame()
		{
			TestFolder folder = new();
			folder.Write("ddraw.ini", "[Main]\r\nHiResMode=1\r\n[Misc]\r\n; a comment\r\nExtraSaveSlots=0\r\nAutoQuickSave=0\r\n[Speed]\r\nEnable=0\r\n");
			folder.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\n");
			return folder;
		}

		private static T Find<T>(MainViewModel viewModel, String id)
			where T : ConfigOption
		{
			return (T)viewModel.AllOptions.First(option => option.Id == id);
		}

		[Fact]
		public void ComparingTwoVersionsFindsTheChangedAddedAndRemovedSettings()
		{
			String[] before = ["[A]", "x=1", "y=2", "[B]", "z=3"];
			String[] after = ["[A]", "x=5", "; y=2", "w=4", "[B]", "z=3"];

			IReadOnlyList<IniChange> changes = IniComparer.Compare(before, after);

			Assert.Contains(new IniChange(ChangeKind.Changed, "A", "x", "1", "5"), changes);
			Assert.Contains(new IniChange(ChangeKind.Removed, "A", "y", "2", null), changes);
			Assert.Contains(new IniChange(ChangeKind.Added, "A", "w", null, "4"), changes);
			Assert.Equal(3, changes.Count);
		}

		[Fact]
		public void AFileThatWasNotChangedHasNoPreview()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder, new TestInteraction());

			Assert.True(viewModel.CreatePreview().IsEmpty);
			Assert.False(viewModel.HasPendingChanges);
		}

		[Fact]
		public void ThePreviewNamesTheOptionsAndShowsWhatTheyWere()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder, new TestInteraction());

			Find<BoolOption>(viewModel, "extraSaveSlots").Value = true;
			Find<NumberOption>(viewModel, "quickSavePages").Value = 5;

			ChangePreview preview = viewModel.CreatePreview();
			ChangeGroup file = Assert.Single(preview.Groups);
			Assert.Equal("ddraw.ini", file.FileName);
			Assert.Equal(2, preview.Count);

			ChangeRow slots = file.Rows.Single(row => row.Location.EndsWith("ExtraSaveSlots", StringComparison.Ordinal));
			Assert.Equal("Disabled (0)", slots.Before);
			Assert.Equal("Enabled (1)", slots.After);
			Assert.NotEqual(slots.Location, slots.Name);
		}

		[Fact]
		public void ThePreviewIsEmptyAgainAfterTheChangeIsUndone()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder, new TestInteraction());
			BoolOption slots = Find<BoolOption>(viewModel, "extraSaveSlots");

			slots.Value = true;
			Assert.False(viewModel.CreatePreview().IsEmpty);
			slots.Undo();

			Assert.True(viewModel.CreatePreview().IsEmpty);
		}

		[Fact]
		public void TheReviewButtonShowsTheChangesWithoutSavingThem()
		{
			using TestFolder folder = CreateGame();
			TestInteraction interaction = new();
			MainViewModel viewModel = Open(folder, interaction);
			Find<BoolOption>(viewModel, "extraSaveSlots").Value = true;

			viewModel.ReviewChangesCommand.Execute(null);

			Assert.Single(interaction.Reviews);
			Assert.Contains("ExtraSaveSlots=0", System.IO.File.ReadAllText(folder.Combine("ddraw.ini")));
		}

		[Fact]
		public void ThePreviewCanStopTheSave()
		{
			using TestFolder folder = CreateGame();
			TestInteraction interaction = new() { ReviewAnswer = false };
			MainViewModel viewModel = Open(folder, interaction);
			viewModel.PreviewBeforeSaving = true;
			Find<BoolOption>(viewModel, "extraSaveSlots").Value = true;

			Assert.False(viewModel.ConfirmClose());
			Assert.Contains("ExtraSaveSlots=0", System.IO.File.ReadAllText(folder.Combine("ddraw.ini")));

			interaction.ReviewAnswer = true;
			Assert.True(viewModel.ConfirmClose());
			Assert.Contains("ExtraSaveSlots=1", System.IO.File.ReadAllText(folder.Combine("ddraw.ini")));
			Assert.False(viewModel.HasPendingChanges);
			viewModel.PreviewBeforeSaving = false;
		}

		[Fact]
		public void NothingIsAskedWhenThePreviewIsOffOrNothingChanged()
		{
			using TestFolder folder = CreateGame();
			TestInteraction interaction = new();
			MainViewModel viewModel = Open(folder, interaction);
			Find<BoolOption>(viewModel, "extraSaveSlots").Value = true;

			Assert.True(viewModel.ConfirmClose());

			Assert.Empty(interaction.Reviews);
		}
	}
}