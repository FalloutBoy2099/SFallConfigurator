namespace SFallConfigurator.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using Configuration;
	using Xunit;

	public class StagedFileTests
	{
		private static readonly Encoding Utf8 = new UTF8Encoding(false);

		[Fact]
		public void TwoFilesStagedForTheSameDestinationDoNotShareATemporaryFile()
		{
			using TestFolder folder = new();
			String path = folder.Write("a.ini", "old");

			StagedFile first = StagedFile.Write(path, ["first"], Utf8);
			StagedFile second = StagedFile.Write(path, ["second"], Utf8);

			Assert.Equal(3, Directory.GetFiles(folder.Path).Length);
			StagedFile.CommitAll([first]);
			second.Discard();
			Assert.Equal(["first"], File.ReadAllLines(path));
			Assert.Equal([path], Directory.GetFiles(folder.Path));
		}

		[Fact]
		public void CommittingSeveralFilesReplacesAllOfThemAndTellsEachOne()
		{
			using TestFolder folder = new();
			String a = folder.Write("a.ini", "old a");
			String b = folder.Combine("b.ini");
			List<String> told = [];

			StagedFile.CommitAll(
			[
				StagedFile.Write(a, ["new a"], Utf8, () => told.Add("a")),
				StagedFile.Write(b, ["new b"], Utf8, () => told.Add("b")),
			]);

			Assert.Equal(["new a"], File.ReadAllLines(a));
			Assert.Equal(["new b"], File.ReadAllLines(b));
			Assert.Equal(["a", "b"], told);
			Assert.Equal(2, Directory.GetFiles(folder.Path).Length);
		}

		[Fact]
		public void WhenOneReplacementFailsTheOthersGetTheirOldContentBack()
		{
			using TestFolder folder = new();
			String a = folder.Write("a.ini", "old a");
			String b = folder.Write("b.ini", "old b");
			String c = folder.Write("c.ini", "old c");
			List<String> told = [];
			StagedFile[] staged =
			[
				StagedFile.Write(a, ["new a"], Utf8, () => told.Add("a")),
				StagedFile.Write(b, ["new b"], Utf8, () => told.Add("b")),
				StagedFile.Write(c, ["new c"], Utf8, () => told.Add("c")),
			];

			// Another program holds the third file open: the replacement of that one fails, after the first two succeeded.
			using (new FileStream(c, FileMode.Open, FileAccess.Read, FileShare.None))
			{
				Assert.ThrowsAny<IOException>(() => StagedFile.CommitAll(staged));
			}

			Assert.Equal(["old a"], File.ReadAllLines(a));
			Assert.Equal(["old b"], File.ReadAllLines(b));
			Assert.Equal(["old c"], File.ReadAllLines(c));
			Assert.Empty(told);
			Assert.Equal(3, Directory.GetFiles(folder.Path).Length);
		}

		[Fact]
		public void AFileThatDidNotExistIsRemovedAgainWhenALaterReplacementFails()
		{
			using TestFolder folder = new();
			String created = folder.Combine("new.ini");
			String locked = folder.Write("locked.ini", "old");
			StagedFile[] staged =
			[
				StagedFile.Write(created, ["new"], Utf8),
				StagedFile.Write(locked, ["changed"], Utf8),
			];

			using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
			{
				Assert.ThrowsAny<IOException>(() => StagedFile.CommitAll(staged));
			}

			Assert.False(File.Exists(created));
			Assert.Equal(["old"], File.ReadAllLines(locked));
			Assert.Equal([locked], Directory.GetFiles(folder.Path));
		}

		[Fact]
		public void AReadOnlyDestinationIsRefusedBeforeAnythingIsWritten()
		{
			using TestFolder folder = new();
			String path = folder.Write("a.ini", "old");
			File.SetAttributes(path, FileAttributes.ReadOnly);

			Assert.Throws<UnauthorizedAccessException>(() => StagedFile.Write(path, ["new"], Utf8));

			Assert.Equal([path], Directory.GetFiles(folder.Path));
		}

		[Fact]
		public void TheAttributesOfTheReplacedFileAreKept()
		{
			using TestFolder folder = new();
			String path = folder.Write("a.ini", "old");
			File.SetAttributes(path, FileAttributes.Hidden | FileAttributes.Archive);

			StagedFile.WriteAllLines(path, ["new"], Utf8);

			Assert.True(File.GetAttributes(path).HasFlag(FileAttributes.Hidden));
		}
	}
}