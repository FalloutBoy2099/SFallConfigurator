namespace SFallConfigurator.Dat
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.IO.Compression;
	using System.Text;
	using Localization;

	/// <summary>
	/// The archive is not a Fallout 2 DAT file (a Fallout 1 DAT, a truncated or a damaged file).
	/// </summary>
	internal sealed class DatFormatException : Exception
	{
		public DatFormatException(String message)
			: base(message)
		{
		}
	}

	/// <summary>
	/// One file stored in a <see cref="DatArchive"/>.
	/// </summary>
	internal sealed class DatEntry
	{
		internal DatEntry(String path, Boolean isCompressed, Int32 unpackedSize, Int32 packedSize, UInt32 offset)
		{
			Path = path;
			IsCompressed = isCompressed;
			UnpackedSize = unpackedSize;
			PackedSize = packedSize;
			Offset = offset;
		}

		internal DatEntry(String path, Byte[] data)
		{
			Path = path;
			PendingData = data;
			UnpackedSize = data.Length;
		}

		/// <summary>
		/// Path inside the archive, with the case it was stored in.
		/// </summary>
		public String Path { get; }

		public Boolean IsCompressed { get; internal set; }

		public Int32 UnpackedSize { get; internal set; }

		public Int32 PackedSize { get; internal set; }

		internal UInt32 Offset { get; set; }

		/// <summary>
		/// Content that is not in the archive file yet (an added or replaced file).
		/// </summary>
		internal Byte[]? PendingData { get; set; }
	}

	/// <summary>
	/// A Fallout 2 DAT archive. Files are only ever appended: saving writes the new content over the old
	/// directory tree and writes a new tree behind it.
	/// <para>Layout: file data, then the tree (file count, one record per file), then the tree size and the archive size.</para>
	/// </summary>
	internal sealed class DatArchive : IDisposable
	{
		private const Int32 FooterSize = 8;
		private const Int32 FileCountSize = 4;
		private const Int32 MinimumRecordSize = 17;

		private readonly FileStream _stream;
		private readonly List<DatEntry> _entries = [];
		private Int32 _treeSize;
		private Int64 _dataEnd;

		private DatArchive(FileStream stream)
		{
			_stream = stream;
			Path = stream.Name;
		}

		public String Path { get; }

		public IReadOnlyList<DatEntry> Entries => _entries;

		public static DatArchive Open(String path, Boolean writable)
		{
			FileStream stream = new(
				path,
				FileMode.Open,
				writable ? FileAccess.ReadWrite : FileAccess.Read,
				FileShare.ReadWrite);

			try
			{
				DatArchive archive = new(stream);
				archive.ReadDirectory();
				return archive;
			}
			catch
			{
				stream.Dispose();
				throw;
			}
		}

		public DatEntry? FindFile(String path)
		{
			String wanted = path.TrimStart('\\');
			return _entries.Find(entry => entry.Path.Equals(wanted, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Returns the uncompressed content of a file.
		/// </summary>
		public Byte[] ReadFile(DatEntry entry)
		{
			if (entry.PendingData is not null)
			{
				return entry.PendingData;
			}

			// The sizes were checked against the archive when the directory was read: the buffer cannot be larger than the file.
			_stream.Seek(entry.Offset, SeekOrigin.Begin);
			Int32 storedSize = entry.IsCompressed ? entry.PackedSize : entry.UnpackedSize;
			Byte[] stored = new Byte[storedSize];
			_stream.ReadExactly(stored, 0, storedSize);

			if (!entry.IsCompressed)
			{
				return stored;
			}

			return Unpack(stored, entry.UnpackedSize);
		}

		/// <summary>
		/// Inflates a zlib stream that must hold exactly <paramref name="expectedSize"/> bytes. The output is never
		/// larger than that, whatever the stream says, so that a crafted entry cannot use all the memory.
		/// </summary>
		private static Byte[] Unpack(Byte[] stored, Int32 expectedSize)
		{
			try
			{
				using MemoryStream source = new(stored);
				using ZLibStream zlib = new(source, CompressionMode.Decompress);
				using MemoryStream output = new();
				Byte[] buffer = new Byte[81920];
				Int64 remaining = expectedSize;
				while (true)
				{
					Int32 read = zlib.Read(buffer, 0, (Int32)Math.Min(buffer.Length, remaining + 1));
					if (read == 0)
					{
						break;
					}

					remaining -= read;
					if (remaining < 0)
					{
						throw new DatFormatException(Localizer.T("The DAT directory is damaged."));
					}

					output.Write(buffer, 0, read);
				}

				if (remaining != 0)
				{
					throw new DatFormatException(Localizer.T("The DAT directory is damaged."));
				}

				return output.ToArray();
			}
			catch (InvalidDataException)
			{
				throw new DatFormatException(Localizer.T("The DAT directory is damaged."));
			}
		}

		/// <summary>
		/// Adds a file, or replaces the content of an existing one. Takes effect on <see cref="Save"/>.
		/// </summary>
		public void SetFile(String path, Byte[] data)
		{
			DatEntry? existing = FindFile(path);
			if (existing is null)
			{
				_entries.Add(new DatEntry(path.TrimStart('\\'), data));
				return;
			}

			existing.PendingData = data;
			existing.UnpackedSize = data.Length;
			existing.PackedSize = 0;
			existing.IsCompressed = false;
		}

		public void Save()
		{
			// The old tree and footer are overwritten: new data starts where the file count was.
			// They are kept in memory until the end, so that a failure puts the archive back as it was.
			Int64 position = _stream.Length - FooterSize - _treeSize;
			Byte[] oldTail = new Byte[_stream.Length - position];
			_stream.Seek(position, SeekOrigin.Begin);
			_stream.ReadExactly(oldTail);
			_stream.Seek(position, SeekOrigin.Begin);

			List<(DatEntry Entry, Byte[]? PendingData, Boolean IsCompressed, Int32 UnpackedSize, Int32 PackedSize, UInt32 Offset)> snapshot =
				_entries.ConvertAll(entry => (entry, entry.PendingData, entry.IsCompressed, entry.UnpackedSize, entry.PackedSize, entry.Offset));
			Int32 treeSize = _treeSize;
			Int64 dataEnd = _dataEnd;

			try
			{
				WriteChanges();
			}
			catch
			{
				// The entries go back to what they were, so that a new attempt starts from a valid state.
				_entries.Clear();
				foreach ((DatEntry entry, Byte[]? pendingData, Boolean isCompressed, Int32 unpackedSize, Int32 packedSize, UInt32 offset) in snapshot)
				{
					entry.PendingData = pendingData;
					entry.IsCompressed = isCompressed;
					entry.UnpackedSize = unpackedSize;
					entry.PackedSize = packedSize;
					entry.Offset = offset;
					_entries.Add(entry);
				}

				_treeSize = treeSize;
				_dataEnd = dataEnd;
				Restore(position, oldTail);
				throw;
			}
		}

		private void Restore(Int64 position, Byte[] oldTail)
		{
			try
			{
				_stream.Seek(position, SeekOrigin.Begin);
				_stream.Write(oldTail, 0, oldTail.Length);
				_stream.SetLength(position + oldTail.Length);
				_stream.Flush();
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ObjectDisposedException)
			{
				// The disk refuses even that; the original error is the one to report.
			}
		}

		private void WriteChanges()
		{
			foreach (DatEntry entry in _entries)
			{
				if (entry.PendingData is null)
				{
					continue;
				}

				Byte[] stored = Pack(entry);
				entry.Offset = checked((UInt32)_stream.Position);
				_stream.Write(stored, 0, stored.Length);
				entry.PendingData = null;
			}

			_entries.Sort((left, right) => String.CompareOrdinal(left.Path.ToLowerInvariant(), right.Path.ToLowerInvariant()));

			using (BinaryWriter writer = new(_stream, Encoding.Latin1, leaveOpen: true))
			{
				writer.Write(_entries.Count);
				Int64 entriesStart = _stream.Position;

				foreach (DatEntry entry in _entries)
				{
					Byte[] name = Encoding.Latin1.GetBytes(entry.Path);
					writer.Write(name.Length);
					writer.Write(name);
					writer.Write((Byte)(entry.IsCompressed ? 1 : 0));
					writer.Write(entry.UnpackedSize);
					writer.Write(entry.PackedSize);
					writer.Write(entry.Offset);
				}

				_treeSize = (Int32)(_stream.Position - entriesStart) + FileCountSize;
				writer.Write(_treeSize);
				writer.Write(checked((UInt32)(_stream.Position + sizeof(UInt32))));
			}

			_dataEnd = _stream.Position - sizeof(Int32) - sizeof(UInt32) - _treeSize;
			_stream.SetLength(_stream.Position);
			_stream.Flush();
		}

		public void Dispose()
		{
			_stream.Dispose();
		}

		/// <summary>
		/// Compresses the pending data when that makes it smaller; updates the entry accordingly.
		/// </summary>
		private static Byte[] Pack(DatEntry entry)
		{
			Byte[] data = entry.PendingData!;

			using MemoryStream compressed = new();
			using (ZLibStream zlib = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
			{
				zlib.Write(data, 0, data.Length);
			}

			entry.UnpackedSize = data.Length;
			if (compressed.Length >= data.Length)
			{
				entry.IsCompressed = false;
				entry.PackedSize = data.Length;
				return data;
			}

			entry.IsCompressed = true;
			entry.PackedSize = (Int32)compressed.Length;
			return compressed.ToArray();
		}

		private void ReadDirectory()
		{
			if (_stream.Length < FooterSize + FileCountSize)
			{
				throw new DatFormatException(Localizer.T("The DAT file is too small."));
			}

			using BinaryReader reader = new(_stream, Encoding.Latin1, leaveOpen: true);

			_stream.Seek(-FooterSize, SeekOrigin.End);
			_treeSize = reader.ReadInt32();
			UInt32 archiveSize = reader.ReadUInt32();

			if (archiveSize != _stream.Length)
			{
				throw new DatFormatException(Localizer.T("The DAT file is not a Fallout 2 archive or its size is incorrect."));
			}

			if (_treeSize < FileCountSize || _treeSize > _stream.Length - FooterSize)
			{
				throw new DatFormatException(Localizer.T("The DAT directory is damaged."));
			}

			_dataEnd = _stream.Length - FooterSize - _treeSize;
			_stream.Seek(_dataEnd, SeekOrigin.Begin);
			Int32 count = reader.ReadInt32();

			// A record takes at least 17 bytes: a count that cannot fit in the tree is a damaged one.
			if (count < 0 || count > (_treeSize - FileCountSize) / MinimumRecordSize)
			{
				throw new DatFormatException(Localizer.T("The DAT directory is damaged."));
			}

			for (Int32 index = 0; index < count; index++)
			{
				Int32 nameLength = reader.ReadInt32();
				if (nameLength < 0 || nameLength > 1024)
				{
					throw new DatFormatException(Localizer.T("The DAT directory is damaged."));
				}

				String name = Encoding.Latin1.GetString(reader.ReadBytes(nameLength));
				Boolean isCompressed = reader.ReadByte() == 1;
				Int32 unpackedSize = reader.ReadInt32();
				Int32 packedSize = reader.ReadInt32();
				UInt32 offset = reader.ReadUInt32();

				// Some tools leave the flag clear but store a packed size.
				if (!isCompressed && unpackedSize != packedSize)
				{
					isCompressed = true;
				}

				// The data of a file lies before the directory: anything else is damaged, or an attempt to read other memory.
				Int64 storedSize = isCompressed ? packedSize : unpackedSize;
				if (unpackedSize < 0 || packedSize < 0 || offset + storedSize > _dataEnd || _stream.Position > _stream.Length - FooterSize)
				{
					throw new DatFormatException(Localizer.T("The DAT directory is damaged."));
				}

				_entries.Add(new DatEntry(name, isCompressed, unpackedSize, packedSize, offset));
			}
		}
	}
}