namespace SFallConfigurator.Services
{
	using System;
	using System.Security.Cryptography;
	using System.Text;
	using System.Threading;

	/// <summary>
	/// Says that this program edits a game folder, so that a second window of the program does not edit it at the same time and
	/// overwrite the changes of the first. The lock is a named mutex of the user session: it goes away with the program, even when
	/// the program ends badly, and it leaves no file in the game folder.
	/// <para>The lock belongs to the thread that took it: release it on that thread.</para>
	/// </summary>
	internal sealed class FolderLock : IDisposable
	{
		private readonly Mutex _mutex;

		private FolderLock(Mutex mutex)
		{
			_mutex = mutex;
		}

		/// <summary>
		/// Takes the lock of the folder. Returns null when another program (or thread) holds it.
		/// </summary>
		public static FolderLock? TryAcquire(String gamePath)
		{
			Mutex mutex = new(initiallyOwned: false, GetName(gamePath));
			try
			{
				if (mutex.WaitOne(0))
				{
					return new FolderLock(mutex);
				}
			}
			catch (AbandonedMutexException)
			{
				// The program that held it ended without releasing it: the lock is ours now.
				return new FolderLock(mutex);
			}

			mutex.Dispose();
			return null;
		}

		public void Dispose()
		{
			try
			{
				_mutex.ReleaseMutex();
			}
			catch (ApplicationException)
			{
				// Not owned by this thread any more: nothing to release.
			}

			_mutex.Dispose();
		}

		/// <summary>
		/// The name of the lock of a folder: the same folder gets the same name however it is written.
		/// </summary>
		public static String GetName(String gamePath)
		{
			String normalized = System.IO.Path.GetFullPath(gamePath).TrimEnd('\\', '/').ToUpperInvariant();
			Byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
			return "Local\\SFallConfigurator-" + Convert.ToHexString(hash, 0, 8);
		}
	}
}