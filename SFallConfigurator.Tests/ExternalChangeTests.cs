namespace SFallConfigurator.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using Configuration;
	using Infrastructure;
	using Options;
	using Services;
	using ViewModels;
	using Xunit;

	/// <summary>
	/// What happens when another program changes the configuration files while the configurator is open.
	/// </summary>
	public sealed class ExternalChangeTests : IDisposable
	{
		private readonly List<IDisposable> _disposables = [];

		public void Dispose()
		{
			_disposables.ForEach(item => item.Dispose());
			UserSettings.Set("AutoReload", null);
		}

		private static TestFolder CreateGame()
		{
			TestFolder folder = new();
			folder.Write("ddraw.ini", "[Main]\r\nHiResMode=1\r\n[Misc]\r\nExtraSaveSlots=0\r\n");
			folder.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\n");
			return folder;
		}

		private MainViewModel Open(TestFolder folder, TestHost host)
		{
			GameConfiguration configuration = new(folder.Path);
			configuration.Load();
			MainViewModel viewModel = new(configuration, new AppServices(new TestInteraction()), host);
			viewModel.Load();
			_disposables.Add(viewModel);
			return viewModel;
		}

		private static void Touch(String path, String content)
		{
			File.WriteAllText(path, content);

			// A file written twice in the same instant could get the same stamp: make the change visible.
			File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));
		}

		[Fact]
		public void OnlyARealChangeOfAFileIsReported()
		{
			using TestFolder folder = CreateGame();
			using ConfigFileWatcher watcher = new(() => [folder.Combine("ddraw.ini"), folder.Combine("fallout2.cfg")], TimeSpan.FromMilliseconds(50));
			watcher.Snapshot();

			Assert.Empty(watcher.FindChanged());
			Touch(folder.Combine("fallout2.cfg"), "[system]\r\nlanguage=french\r\n");

			Assert.Equal([folder.Combine("fallout2.cfg")], watcher.FindChanged());
			watcher.Snapshot();
			Assert.Empty(watcher.FindChanged());
		}

		[Fact]
		public void ACreatedOrDeletedFileIsAChange()
		{
			using TestFolder folder = CreateGame();
			using ConfigFileWatcher watcher = new(() => [folder.Combine("ddraw.ini"), folder.Combine("sfall.ini")], TimeSpan.FromMilliseconds(50));
			watcher.Snapshot();

			folder.Write("sfall.ini", "[Misc]\r\n");
			Assert.Equal([folder.Combine("sfall.ini")], watcher.FindChanged());

			watcher.Snapshot();
			File.Delete(folder.Combine("ddraw.ini"));
			Assert.Equal([folder.Combine("ddraw.ini")], watcher.FindChanged());
		}

		[Fact]
		public void TheWatcherReportsAChangeOnceAfterAQuietTime()
		{
			using TestFolder folder = CreateGame();
			using ConfigFileWatcher watcher = new(() => [folder.Combine("ddraw.ini")], TimeSpan.FromMilliseconds(100));
			List<String> reported = [];
			using ManualResetEventSlim signal = new();
			watcher.Changed += (_, files) =>
			{
				lock (reported)
				{
					reported.AddRange(files);
				}

				signal.Set();
			};
			watcher.Start();

			Touch(folder.Combine("ddraw.ini"), "[Misc]\r\nExtraSaveSlots=1\r\n");
			Touch(folder.Combine("ddraw.ini"), "[Misc]\r\nExtraSaveSlots=1\r\nAutoQuickSave=2\r\n");

			Assert.True(signal.Wait(TimeSpan.FromSeconds(10)));
			Thread.Sleep(400);
			lock (reported)
			{
				Assert.Equal([folder.Combine("ddraw.ini")], reported);
			}
		}

		[Fact]
		public void TheOwnSavesOfTheProgramAreNotChanges()
		{
			using TestFolder folder = CreateGame();
			using ConfigFileWatcher watcher = new(() => [folder.Combine("ddraw.ini")], TimeSpan.FromMilliseconds(50));
			Int32 count = 0;
			watcher.Changed += (_, _) => Interlocked.Increment(ref count);
			watcher.Start();

			watcher.Suspend();
			Touch(folder.Combine("ddraw.ini"), "[Misc]\r\nExtraSaveSlots=1\r\n");
			Thread.Sleep(400);
			watcher.Resume();
			Thread.Sleep(400);

			Assert.Equal(0, count);
			Assert.Empty(watcher.FindChanged());
		}

		[Fact]
		public void WithNothingToLoseTheFolderIsReadAgainAtOnce()
		{
			using TestFolder folder = CreateGame();
			TestHost host = new();
			MainViewModel viewModel = Open(folder, host);

			viewModel.HandleExternalChange([folder.Combine("ddraw.ini")]);

			Assert.Equal(1, host.ReloadCount);
			Assert.Contains("ddraw.ini", host.ReloadNotices.Single());
			Assert.False(viewModel.HasNotice);
		}

		[Fact]
		public void WithUnsavedChangesTheUserIsAskedInsteadOfLosingThem()
		{
			using TestFolder folder = CreateGame();
			TestHost host = new();
			MainViewModel viewModel = Open(folder, host);
			viewModel.AllOptions.OfType<BoolOption>().First(option => option.Id == "extraSaveSlots").Value = true;

			viewModel.HandleExternalChange([folder.Combine("ddraw.ini")]);

			Assert.Equal(0, host.ReloadCount);
			Assert.True(viewModel.HasNotice);
			Assert.True(viewModel.CanReloadNow);
			Assert.Contains("ddraw.ini", viewModel.Notice);

			viewModel.ReloadNowCommand.Execute(null);
			Assert.Equal(1, host.ReloadCount);
		}

		[Fact]
		public void TheAutomaticReloadCanBeTurnedOff()
		{
			using TestFolder folder = CreateGame();
			TestHost host = new();
			MainViewModel viewModel = Open(folder, host);
			viewModel.AutoReload = false;

			viewModel.HandleExternalChange([folder.Combine("fallout2.cfg")]);

			Assert.Equal(0, host.ReloadCount);
			Assert.True(viewModel.CanReloadNow);
			viewModel.DismissNoticeCommand.Execute(null);
			Assert.False(viewModel.HasNotice);
		}

		[Fact]
		public void TheViewModelNoticesAFileThatAnotherProgramChanged()
		{
			using TestFolder folder = CreateGame();
			TestHost host = new();
			MainViewModel viewModel = Open(folder, host);
			viewModel.StartWatching(TimeSpan.FromMilliseconds(100));

			Touch(folder.Combine("ddraw.ini"), "[Misc]\r\nExtraSaveSlots=1\r\n");

			for (Int32 attempt = 0; attempt < 100 && host.ReloadCount == 0; attempt++)
			{
				Thread.Sleep(100);
			}

			Assert.Equal(1, host.ReloadCount);
		}

		[Fact]
		public void SavingDoesNotCauseAReload()
		{
			using TestFolder folder = CreateGame();
			TestHost host = new();
			MainViewModel viewModel = Open(folder, host);
			viewModel.StartWatching(TimeSpan.FromMilliseconds(100));
			viewModel.AllOptions.OfType<BoolOption>().First(option => option.Id == "extraSaveSlots").Value = true;

			Assert.True(viewModel.ConfirmClose());
			Thread.Sleep(800);

			Assert.Equal(0, host.ReloadCount);
			Assert.False(viewModel.HasNotice);
		}

		[Fact]
		public void TheTrackedFilesAreTheOnesTheProgramWrites()
		{
			using TestFolder folder = CreateGame();
			GameConfiguration configuration = new(folder.Path);
			configuration.Load();

			IReadOnlyList<String> files = configuration.GetTrackedFiles();

			Assert.Contains(folder.Combine("ddraw.ini"), files);
			Assert.Contains(folder.Combine("fallout2.cfg"), files);
			Assert.Contains(Path.Combine(folder.Path, "mods", "mods_order.ini"), files);
		}
	}
}