namespace SFallConfigurator.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using Localization;

	/// <summary>
	/// A file that was written next to its destination and waits to replace it. Until it is committed the
	/// destination is untouched, so a failure while writing (disk full, locked file) cannot truncate it.
	/// Several staged files can be committed together: if one of them fails, the ones already replaced are put back.
	/// </summary>
	internal sealed class StagedFile
	{
		private readonly String _temporaryPath;
		private readonly String _path;
		private readonly Action? _committed;

		private String? _backupPath;
		private Boolean _isReplaced;

		private StagedFile(String temporaryPath, String path, Action? committed)
		{
			_temporaryPath = temporaryPath;
			_path = path;
			_committed = committed;
		}

		/// <summary>
		/// The file this staged file will replace.
		/// </summary>
		public String Destination => _path;

		/// <summary>
		/// Writes the lines to a temporary file next to <paramref name="path"/>.
		/// </summary>
		/// <param name="committed">Called after the destination was replaced.</param>
		public static StagedFile Write(String path, IEnumerable<String> lines, Encoding encoding, Action? committed = null)
		{
			String? directory = System.IO.Path.GetDirectoryName(path);
			if (!String.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}

			// A read-only file is refused now, before anything else is written, so that a group of files fails as a whole.
			if (File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly))
			{
				throw new UnauthorizedAccessException(Localizer.F("Access to the path '{0}' is denied: the file is read-only.", path));
			}

			// The name is unique, so that two files staged for the same destination cannot overwrite each other.
			String temporaryPath = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
			try
			{
				File.WriteAllLines(temporaryPath, lines, encoding);
			}
			catch
			{
				DeleteQuietly(temporaryPath);
				throw;
			}

			return new StagedFile(temporaryPath, path, committed);
		}

		/// <summary>
		/// Writes a file in one step: the old content stays until the new one is complete.
		/// </summary>
		public static void WriteAllLines(String path, IEnumerable<String> lines, Encoding encoding)
		{
			Write(path, lines, encoding).Commit();
		}

		/// <summary>
		/// Replaces the destinations with the staged files, all or none: when one replacement fails, the destinations
		/// already replaced get their old content back and the other staged files are thrown away.
		/// </summary>
		public static void CommitAll(IEnumerable<StagedFile> files)
		{
			List<StagedFile> pending = files.ToList();
			List<StagedFile> done = [];
			try
			{
				foreach (StagedFile file in pending)
				{
					file.Replace();
					done.Add(file);
				}
			}
			catch
			{
				for (Int32 index = done.Count - 1; index >= 0; index--)
				{
					done[index].Rollback();
				}

				pending.ForEach(file => file.Discard());
				throw;
			}

			foreach (StagedFile file in done)
			{
				file.Finish();
			}
		}

		/// <summary>
		/// Replaces the destination with the staged file.
		/// </summary>
		public void Commit()
		{
			CommitAll([this]);
		}

		/// <summary>
		/// Throws the staged file away.
		/// </summary>
		public void Discard()
		{
			DeleteQuietly(_temporaryPath);
		}

		private static void DeleteQuietly(String path)
		{
			try
			{
				File.Delete(path);
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				// A leftover temporary file is harmless.
			}
		}

		/// <summary>
		/// Puts the staged content in place and keeps the old content aside until <see cref="Finish"/>.
		/// </summary>
		private void Replace()
		{
			if (File.Exists(_path))
			{
				_backupPath = _temporaryPath + ".old";

				// Keeps the attributes and the permissions of the file that is replaced.
				File.Replace(_temporaryPath, _path, _backupPath);
			}
			else
			{
				File.Move(_temporaryPath, _path);
			}

			_isReplaced = true;
		}

		private void Rollback()
		{
			if (!_isReplaced)
			{
				return;
			}

			try
			{
				if (_backupPath is not null)
				{
					File.Move(_backupPath, _path, overwrite: true);
				}
				else
				{
					File.Delete(_path);
				}
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				// The file stays as it is: the original error is the one to report.
			}
		}

		private void Finish()
		{
			if (_backupPath is not null)
			{
				DeleteQuietly(_backupPath);
			}

			_committed?.Invoke();
		}
	}
}