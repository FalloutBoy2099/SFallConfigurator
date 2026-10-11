namespace SFallConfigurator.Tests
{
	using System;
	using System.IO;
	using System.IO.Compression;
	using System.Linq;
	using System.Text;
	using Dat;
	using Xunit;

	public class DatArchiveTests
	{
		/// <summary>
		/// Builds a small Fallout 2 DAT with the given (name, content, compressed) files.
		/// </summary>
		private static void WriteDat(String path, params (String Name, Byte[] Data, Boolean Compress)[] files)
		{
			using FileStream stream = File.Create(path);
			using BinaryWriter writer = new(stream, Encoding.Latin1);

			(String Name, Int32 Unpacked, Int32 Packed, Boolean Compressed, UInt32 Offset)[] entries = new (String, Int32, Int32, Boolean, UInt32)[files.Length];
			for (Int32 index = 0; index < files.Length; index++)
			{
				Byte[] stored = files[index].Data;
				if (files[index].Compress)
				{
					using MemoryStream packed = new();
					using (ZLibStream zlib = new(packed, CompressionLevel.Optimal, leaveOpen: true))
					{
						zlib.Write(stored, 0, stored.Length);
					}

					stored = packed.ToArray();
				}

				entries[index] = (files[index].Name, files[index].Data.Length, stored.Length, files[index].Compress, (UInt32)stream.Position);
				writer.Write(stored);
			}

			writer.Write(files.Length);
			Int64 treeStart = stream.Position;
			foreach ((String name, Int32 unpacked, Int32 packed, Boolean compressed, UInt32 offset) in entries)
			{
				writer.Write(name.Length);
				writer.Write(Encoding.Latin1.GetBytes(name));
				writer.Write((Byte)(compressed ? 1 : 0));
				writer.Write(unpacked);
				writer.Write(packed);
				writer.Write(offset);
			}

			writer.Write((Int32)(stream.Position - treeStart) + 4);
			writer.Write((UInt32)(stream.Position + 4));
		}

		[Fact]
		public void ReadsCompressedAndStoredFiles()
		{
			using TestFolder folder = new();
			String path = folder.Combine("test.dat");
			Byte[] big = Encoding.ASCII.GetBytes(new String('A', 5000));
			WriteDat(path, ("art\\a.frm", big, true), ("text\\b.msg", Encoding.ASCII.GetBytes("hello"), false));

			using DatArchive archive = DatArchive.Open(path, writable: false);

			Assert.Equal(2, archive.Entries.Count);
			Assert.Equal(big, archive.ReadFile(archive.FindFile("ART\\A.FRM")!));
			Assert.Equal("hello", Encoding.ASCII.GetString(archive.ReadFile(archive.FindFile("text\\b.msg")!)));
			Assert.Null(archive.FindFile("nothing"));
		}

		[Fact]
		public void RejectsFilesThatAreNotFallout2Archives()
		{
			using TestFolder folder = new();
			String path = folder.Write("junk.dat", "this is not an archive at all");

			Assert.Throws<DatFormatException>(() => DatArchive.Open(path, writable: false));
		}

		[Fact]
		public void SavedChangesKeepExistingFilesAndAreReadBack()
		{
			using TestFolder folder = new();
			String path = folder.Combine("test.dat");
			Byte[] big = new Byte[3000];
			new Random(1).NextBytes(big);
			WriteDat(path, ("b.bin", big, false), ("a.txt", Encoding.ASCII.GetBytes(new String('x', 800)), true));

			using (DatArchive archive = DatArchive.Open(path, writable: true))
			{
				archive.SetFile("desc.id", Encoding.UTF8.GetBytes("test.dat\r\nМой мод"));
				archive.Save();
			}

			using DatArchive reopened = DatArchive.Open(path, writable: false);
			Assert.Equal(3, reopened.Entries.Count);
			Assert.Equal(big, reopened.ReadFile(reopened.FindFile("b.bin")!));
			Assert.Equal(new String('x', 800), Encoding.ASCII.GetString(reopened.ReadFile(reopened.FindFile("a.txt")!)));
			Assert.Equal("test.dat\r\nМой мод", Encoding.UTF8.GetString(reopened.ReadFile(reopened.FindFile("desc.id")!)));

			// The directory is sorted the way the game searches it.
			Assert.Equal(["a.txt", "b.bin", "desc.id"], reopened.Entries.Select(entry => entry.Path));
		}

		[Fact]
		public void ReplacingAFileKeepsASingleEntry()
		{
			using TestFolder folder = new();
			String path = folder.Combine("test.dat");
			WriteDat(path, ("desc.id", Encoding.ASCII.GetBytes("a\r\nold"), false));

			using (DatArchive archive = DatArchive.Open(path, writable: true))
			{
				archive.SetFile("DESC.ID", Encoding.ASCII.GetBytes("a\r\nnew"));
				archive.Save();
			}

			using DatArchive reopened = DatArchive.Open(path, writable: false);
			Assert.Single(reopened.Entries);
			Assert.Equal("a\r\nnew", Encoding.ASCII.GetString(reopened.ReadFile(reopened.Entries[0])));
		}

		/// <summary>
		/// Optional check against real archives: set SF_CONFIGURATOR_TEST_DAT to a folder holding .dat files.
		/// Every file of every archive must unpack to its stated size.
		/// </summary>
		[RealDataFact("SF_CONFIGURATOR_TEST_DAT")]
		public void RealArchivesUnpackToTheirStatedSize()
		{
			String? folder = Environment.GetEnvironmentVariable("SF_CONFIGURATOR_TEST_DAT");
			if (String.IsNullOrEmpty(folder) || !Directory.Exists(folder))
			{
				return;
			}

			foreach (String path in Directory.GetFiles(folder, "*.dat"))
			{
				using DatArchive archive = DatArchive.Open(path, writable: false);
				foreach (DatEntry entry in archive.Entries)
				{
					Assert.Equal(entry.UnpackedSize, archive.ReadFile(entry).Length);
				}
			}
		}

		[Fact]
		public void DescriptionRoundTrips()
		{
			using TestFolder folder = new();
			String path = folder.Combine("mod.dat");
			WriteDat(path, ("x.txt", Encoding.ASCII.GetBytes("x"), false));

			Assert.Null(DatDescription.Read(path));
			DatDescription.Write(path, "Мой мод v2");
			Assert.Equal("Мой мод v2", DatDescription.Read(path));
			DatDescription.Write(path, "second");
			Assert.Equal("second", DatDescription.Read(path));
		}
	}
}