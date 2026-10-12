namespace SFallConfigurator.Services
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;

	/// <summary>
	/// Notices that the configuration files were changed by another program (an editor, another tool, a restored backup).
	/// <para>
	/// The watchers of the system only say that something happened in a folder, often several times. The files are therefore
	/// compared with what they were when the program read or saved them: only a real change is reported, once, after a short quiet
	/// time. The program's own saves are not changes (see <see cref="Suspend"/>).
	/// </para>
	/// </summary>
	internal sealed class ConfigFileWatcher : IDisposable
	{
		private readonly Func<IEnumerable<String>> _files;
		private readonly TimeSpan _quietTime;
		private readonly List<FileSystemWatcher> _watchers = [];
		private readonly Timer _timer;
		private readonly Object _lock = new();

		private Dictionary<String, FileStamp> _stamps = new(StringComparer.OrdinalIgnoreCase);
		private Int32 _suspended;
		private Boolean _isDisposed;

		public ConfigFileWatcher(Func<IEnumerable<String>> files, TimeSpan quietTime)
		{
			_files = files;
			_quietTime = quietTime;
			_timer = new Timer(_ => OnQuiet());
		}

		/// <summary>
		/// Raised, on a thread of the pool, with the paths of the files that changed.
		/// </summary>
		public event EventHandler<IReadOnlyList<String>>? Changed;

		/// <summary>
		/// Remembers the files as they are, and starts to watch their folders.
		/// </summary>
		public void Start()
		{
			Snapshot();
			foreach (String directory in _files().Select(Path.GetDirectoryName).OfType<String>().Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists))
			{
				try
				{
					FileSystemWatcher watcher = new(directory) { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName, EnableRaisingEvents = false };
					watcher.Changed += OnFileEvent;
					watcher.Created += OnFileEvent;
					watcher.Deleted += OnFileEvent;
					watcher.Renamed += OnFileEvent;
					watcher.EnableRaisingEvents = true;
					_watchers.Add(watcher);
				}
				catch (Exception exception) when (exception is IOException or ArgumentException or PlatformNotSupportedException)
				{
					// A folder that cannot be watched only loses the automatic reload.
				}
			}
		}

		/// <summary>
		/// Takes the present state of the files as the one the program knows.
		/// </summary>
		public void Snapshot()
		{
			Dictionary<String, FileStamp> stamps = new(StringComparer.OrdinalIgnoreCase);
			foreach (String file in _files())
			{
				stamps[file] = FileStamp.Of(file);
			}

			lock (_lock)
			{
				_stamps = stamps;
			}
		}

		/// <summary>
		/// The files that are not as they were at the last snapshot.
		/// </summary>
		public IReadOnlyList<String> FindChanged()
		{
			Dictionary<String, FileStamp> known;
			lock (_lock)
			{
				known = _stamps;
			}

			return _files().Where(file => !known.TryGetValue(file, out FileStamp stamp) || stamp != FileStamp.Of(file)).ToList();
		}

		/// <summary>
		/// The program is about to write the files: what happens to them meanwhile is not a change.
		/// </summary>
		public void Suspend()
		{
			Interlocked.Increment(ref _suspended);
		}

		/// <summary>
		/// The program wrote its files: they are the new known state.
		/// </summary>
		/// <param name="takeSnapshot">False when nothing was written: a change that happened meanwhile is still a change.</param>
		public void Resume(Boolean takeSnapshot = true)
		{
			if (takeSnapshot)
			{
				Snapshot();
			}

			Interlocked.Decrement(ref _suspended);
		}

		public void Dispose()
		{
			_isDisposed = true;
			_timer.Dispose();
			foreach (FileSystemWatcher watcher in _watchers)
			{
				watcher.Dispose();
			}

			_watchers.Clear();
		}

		private void OnFileEvent(Object sender, FileSystemEventArgs e)
		{
			if (!_isDisposed)
			{
				try
				{
					_timer.Change(_quietTime, Timeout.InfiniteTimeSpan);
				}
				catch (ObjectDisposedException)
				{
					// Disposed meanwhile: nothing to report.
				}
			}
		}

		private void OnQuiet()
		{
			if (_isDisposed || Volatile.Read(ref _suspended) > 0)
			{
				return;
			}

			IReadOnlyList<String> changed = FindChanged();
			if (changed.Count > 0)
			{
				// What was reported is known now: the next report is about a new change.
				Snapshot();
				Changed?.Invoke(this, changed);
			}
		}

		private readonly record struct FileStamp(Boolean Exists, Int64 LastWriteTicks, Int64 Length)
		{
			public static FileStamp Of(String path)
			{
				try
				{
					FileInfo info = new(path);
					return info.Exists ? new FileStamp(true, info.LastWriteTimeUtc.Ticks, info.Length) : new FileStamp(false, 0, 0);
				}
				catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
				{
					return new FileStamp(false, 0, 0);
				}
			}
		}
	}
}