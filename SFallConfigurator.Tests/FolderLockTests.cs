namespace SFallConfigurator.Tests
{
	using System;
	using System.Threading;
	using Services;
	using Xunit;

	/// <summary>
	/// The lock that keeps two windows from editing the same game folder.
	/// </summary>
	public sealed class FolderLockTests
	{
		/// <summary>
		/// Takes the lock on another thread, as another window (another program) would.
		/// </summary>
		private static Boolean CanAcquireElsewhere(String path)
		{
			Boolean acquired = false;
			Thread thread = new(() =>
			{
				using FolderLock? taken = FolderLock.TryAcquire(path);
				acquired = taken is not null;
			});
			thread.Start();
			thread.Join();
			return acquired;
		}

		private static String NewPath()
		{
			return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SFallConfiguratorTests", "lock-" + Guid.NewGuid().ToString("N"));
		}

		[Fact]
		public void AFolderThatIsLockedCannotBeLockedAgain()
		{
			String path = NewPath();
			using FolderLock? first = FolderLock.TryAcquire(path);

			Assert.NotNull(first);
			Assert.False(CanAcquireElsewhere(path));
		}

		[Fact]
		public void AFolderIsFreeAgainWhenTheLockIsReleased()
		{
			String path = NewPath();
			FolderLock? first = FolderLock.TryAcquire(path);

			first!.Dispose();

			Assert.True(CanAcquireElsewhere(path));
		}

		[Fact]
		public void OtherFoldersAreNotAffected()
		{
			using FolderLock? first = FolderLock.TryAcquire(NewPath());

			Assert.True(CanAcquireElsewhere(NewPath()));
		}

		[Fact]
		public void TheSameFolderHasTheSameNameWhateverTheWayItIsWritten()
		{
			String path = NewPath();

			Assert.Equal(FolderLock.GetName(path), FolderLock.GetName(path.ToUpperInvariant() + "\\"));
			Assert.Equal(FolderLock.GetName(path), FolderLock.GetName(System.IO.Path.Combine(path, "..", System.IO.Path.GetFileName(path))));
			Assert.StartsWith("Local\\", FolderLock.GetName(path), StringComparison.Ordinal);
		}
	}
}