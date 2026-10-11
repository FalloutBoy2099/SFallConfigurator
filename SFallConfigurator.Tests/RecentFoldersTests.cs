namespace SFallConfigurator.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Configuration;
	using Xunit;

	/// <summary>
	/// The settings location is shared state: these tests must not run in parallel with each other.
	/// </summary>
	public sealed class RecentFoldersTests : IDisposable
	{
		private readonly TestFolder _settings = new TestFolder();

		public RecentFoldersTests()
		{
			UserSettings.DirectoryOverride = _settings.Path;
		}

		public void Dispose()
		{
			UserSettings.DirectoryOverride = null;
			_settings.Dispose();
		}

		private static String CreateGame(TestFolder root, String name)
		{
			String path = root.Combine(name);
			Directory.CreateDirectory(path);
			File.WriteAllText(Path.Combine(path, "ddraw.ini"), "[Main]");
			return path;
		}

		[Fact]
		public void RemembersGameFoldersMostRecentFirst()
		{
			using TestFolder games = new TestFolder();
			String first = CreateGame(games, "one");
			String second = CreateGame(games, "two");

			GameLocator.RememberGamePath(first);
			GameLocator.RememberGamePath(second);

			Assert.Equal([second, first], GameLocator.GetRecentFolders());
		}

		[Fact]
		public void OpeningAFolderAgainMovesItToTheTopWithoutDuplicate()
		{
			using TestFolder games = new TestFolder();
			String first = CreateGame(games, "one");
			String second = CreateGame(games, "two");

			GameLocator.RememberGamePath(first);
			GameLocator.RememberGamePath(second);
			GameLocator.RememberGamePath(first.ToUpperInvariant() + "\\");

			Assert.Equal([Path.GetFullPath(first.ToUpperInvariant()), second], GameLocator.GetRecentFolders());
		}

		[Fact]
		public void OnlyTheTenLastFoldersAreKept()
		{
			using TestFolder games = new TestFolder();
			String[] folders = Enumerable.Range(1, 12).Select(index => CreateGame(games, "game" + index)).ToArray();

			foreach (String folder in folders)
			{
				GameLocator.RememberGamePath(folder);
			}

			IReadOnlyList<String> recent = GameLocator.GetRecentFolders();
			Assert.Equal(GameLocator.MaximumRecentFolders, recent.Count);
			Assert.Equal(folders[11], recent[0]);
			Assert.Equal(folders[2], recent[9]);
		}

		[Fact]
		public void FoldersWithoutAGameAreNotRemembered()
		{
			using TestFolder games = new TestFolder();
			String empty = games.Combine("empty");
			Directory.CreateDirectory(empty);

			GameLocator.RememberGamePath(empty);

			Assert.Empty(GameLocator.GetRecentFolders());
		}

		[Fact]
		public void DeletedFoldersDisappearFromTheList()
		{
			using TestFolder games = new TestFolder();
			String kept = CreateGame(games, "kept");
			String gone = CreateGame(games, "gone");
			GameLocator.RememberGamePath(kept);
			GameLocator.RememberGamePath(gone);

			Directory.Delete(gone, recursive: true);

			Assert.Equal([kept], GameLocator.GetRecentFolders());
		}

		[Fact]
		public void TheMostRecentFolderIsOpenedWhenTheProgramIsNotInAGameFolder()
		{
			using TestFolder games = new TestFolder();
			String game = CreateGame(games, "game");
			GameLocator.RememberGamePath(game);

			Assert.Equal(game, GameLocator.Resolve([]));
		}
	}
}