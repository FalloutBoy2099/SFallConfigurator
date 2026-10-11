namespace SFallConfigurator.Tests
{
	using System;
	using System.IO;
	using System.IO.Compression;
	using System.Text;
	using Dat;
	using Xunit;

	/// <summary>
	/// Archives come from mods that anyone can publish: a damaged or crafted directory must be refused with a clear
	/// error, never crash the program or use all the memory.
	/// </summary>
	public class DatCorruptionTests
	{
		private sealed record Record(String Name, Boolean IsCompressed, Int32 UnpackedSize, Int32 PackedSize, UInt32 Offset);

		/// <summary>
		/// Writes an archive whose data and directory records are exactly what the test says, true or not.
		/// </summary>
		private static String Write(TestFolder folder, Byte[] data, Int32? count, params Record[] records)
		{
			String path = folder.Combine("test.dat");
			using FileStream stream = File.Create(path);
			using BinaryWriter writer = new(stream, Encoding.Latin1);
			writer.Write(data);

			Int64 treeStart = stream.Position;
			writer.Write(count ?? records.Length);
			foreach (Record record in records)
			{
				writer.Write(record.Name.Length);
				writer.Write(Encoding.Latin1.GetBytes(record.Name));
				writer.Write((Byte)(record.IsCompressed ? 1 : 0));
				writer.Write(record.UnpackedSize);
				writer.Write(record.PackedSize);
				writer.Write(record.Offset);
			}

			// The size of the directory includes the file count.
			writer.Write((Int32)(stream.Position - treeStart));
			writer.Write((UInt32)(stream.Position + 4));
			return path;
		}

		private static Byte[] Compress(Byte[] data)
		{
			using MemoryStream packed = new();
			using (ZLibStream zlib = new(packed, CompressionLevel.SmallestSize, leaveOpen: true))
			{
				zlib.Write(data, 0, data.Length);
			}

			return packed.ToArray();
		}

		[Theory]
		[InlineData(Int32.MaxValue)]
		[InlineData(-1)]
		[InlineData(1000)]
		public void ACountThatCannotFitInTheDirectoryIsRefused(Int32 count)
		{
			using TestFolder folder = new();
			String path = Write(folder, [1, 2, 3], count, new Record("a.txt", false, 3, 3, 0));

			Assert.Throws<DatFormatException>(() => DatArchive.Open(path, writable: false));
		}

		[Theory]
		[InlineData(-1, 0, 0)]
		[InlineData(3, -5, 0)]
		[InlineData(3, 3, 1000)]
		[InlineData(Int32.MaxValue, Int32.MaxValue, 0)]
		public void SizesAndOffsetsOutsideTheDataAreRefused(Int32 unpacked, Int32 packed, UInt32 offset)
		{
			using TestFolder folder = new();
			String path = Write(folder, [1, 2, 3], null, new Record("a.txt", false, unpacked, packed, offset));

			Assert.Throws<DatFormatException>(() => DatArchive.Open(path, writable: false));
		}

		[Fact]
		public void ADamagedCompressedFileIsReportedWhenItIsRead()
		{
			using TestFolder folder = new();
			Byte[] garbage = [0x78, 0x9C, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];
			String path = Write(folder, garbage, null, new Record("a.txt", true, 100, garbage.Length, 0));

			using DatArchive archive = DatArchive.Open(path, writable: false);

			Assert.Throws<DatFormatException>(() => archive.ReadFile(archive.Entries[0]));
		}

		[Fact]
		public void ACompressedFileLargerThanItsStatedSizeIsRefused()
		{
			using TestFolder folder = new();
			Byte[] packed = Compress(new Byte[10_000_000]);
			String path = Write(folder, packed, null, new Record("bomb.bin", true, 100, packed.Length, 0));

			using DatArchive archive = DatArchive.Open(path, writable: false);

			Assert.Throws<DatFormatException>(() => archive.ReadFile(archive.Entries[0]));
		}

		[Fact]
		public void ACompressedFileSmallerThanItsStatedSizeIsRefused()
		{
			using TestFolder folder = new();
			Byte[] packed = Compress(new Byte[50]);
			String path = Write(folder, packed, null, new Record("short.bin", true, 100, packed.Length, 0));

			using DatArchive archive = DatArchive.Open(path, writable: false);

			Assert.Throws<DatFormatException>(() => archive.ReadFile(archive.Entries[0]));
		}

		[Fact]
		public void AFileThatIsExactlyAsLargeAsItsStatedSizeIsRead()
		{
			using TestFolder folder = new();
			Byte[] content = new Byte[300];
			new Random(1).NextBytes(content);
			Byte[] packed = Compress(content);
			String path = Write(folder, packed, null, new Record("ok.bin", true, content.Length, packed.Length, 0));

			using DatArchive archive = DatArchive.Open(path, writable: false);

			Assert.Equal(content, archive.ReadFile(archive.Entries[0]));
		}

		[Fact]
		public void SavingAFileAndReadingItBackKeepsTheOtherFiles()
		{
			using TestFolder folder = new();
			Byte[] first = Encoding.ASCII.GetBytes("first file");
			String path = Write(folder, first, null, new Record("a.txt", false, first.Length, first.Length, 0));

			using (DatArchive archive = DatArchive.Open(path, writable: true))
			{
				archive.SetFile("b.txt", Encoding.ASCII.GetBytes("second"));
				archive.Save();
			}

			using DatArchive reopened = DatArchive.Open(path, writable: false);
			Assert.Equal(2, reopened.Entries.Count);
			Assert.Equal(first, reopened.ReadFile(reopened.FindFile("a.txt")!));
			Assert.Equal("second", Encoding.ASCII.GetString(reopened.ReadFile(reopened.FindFile("b.txt")!)));
		}
	}
}