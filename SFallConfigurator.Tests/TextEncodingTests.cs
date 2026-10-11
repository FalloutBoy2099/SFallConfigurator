namespace SFallConfigurator.Tests
{
	using System;
	using System.IO;
	using System.Text;
	using Configuration;
	using Xunit;

	public class TextEncodingTests
	{
		[Theory]
		[InlineData(1250)]
		[InlineData(1251)]
		[InlineData(1252)]
		[InlineData(65001)]
		public void TheLegacyFilesAreNeverWrittenWithAByteOrderMark(Int32 codePage)
		{
			using TestFolder folder = new TestFolder();
			Encoding encoding = TextEncodings.CreateAnsiEncoding(codePage);
			String path = folder.Combine("fallout2.cfg");

			StagedFile.WriteAllLines(path, ["[system]", "language=english"], encoding);

			Byte[] bytes = File.ReadAllBytes(path);
			Assert.Equal((Byte)'[', bytes[0]);
		}

		[Fact]
		public void TheUtf8CodePageKeepsItsCharacters()
		{
			Encoding encoding = TextEncodings.CreateAnsiEncoding(65001);

			Assert.Equal("Игры é", encoding.GetString(encoding.GetBytes("Игры é")));
		}

		[Fact]
		public void ASingleByteCodePageKeepsEveryByte()
		{
			Encoding encoding = TextEncodings.CreateAnsiEncoding(1252);
			Byte[] all = new Byte[256];
			for (Int32 index = 0; index < all.Length; index++)
			{
				all[index] = (Byte)index;
			}

			Assert.Equal(all, encoding.GetBytes(encoding.GetString(all)));
		}

		[Fact]
		public void TheSystemCodePageNeverWritesAByteOrderMark()
		{
			Assert.Empty(TextEncodings.Ansi.GetPreamble());
		}
	}
}