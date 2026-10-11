namespace SFallConfigurator.Tests
{
	using System;
	using System.Text;
	using Configuration;
	using Xunit;

	public class IniDocumentTests
	{
		private static IniDocument Create(params String[] lines)
		{
			return new IniDocument("test.ini", lines, Encoding.UTF8);
		}

		[Fact]
		public void GlobalLookupFindsKeyInAnySectionIgnoringCase()
		{
			IniDocument ini = Create("[Main]", "Mode = 4", "[Misc]", "ExtraCRC=0x1");

			Assert.Equal("4", ini.GetValue(null, "mode"));
			Assert.Equal("0x1", ini.GetValue(null, "EXTRACRC"));
			Assert.Null(ini.GetValue(null, "Missing"));
		}

		[Fact]
		public void SectionLookupStopsAtTheNextSection()
		{
			IniDocument ini = Create("[A]", "Key=1", "[B]", "Other=2");

			Assert.Equal("1", ini.GetValue("A", "Key"));
			Assert.Null(ini.GetValue("A", "Other"));
			Assert.Equal("2", ini.GetValue("[b]", "other"));
		}

		[Fact]
		public void CommentedKeysAreNotValues()
		{
			IniDocument ini = Create("[A]", ";Key=1", "Other=2");

			Assert.Null(ini.GetValue("A", "Key"));
			Assert.Equal("2", ini.GetValue("A", "Other"));
		}

		[Fact]
		public void EmptyValueIsReturnedAsEmptyText()
		{
			IniDocument ini = Create("[A]", "Key=");

			Assert.Equal(String.Empty, ini.GetValue("A", "Key"));
		}

		[Fact]
		public void SetValueKeepsTheKeySpellingAndMarksTheDocumentModified()
		{
			IniDocument ini = Create("[A]", "Key = 1");

			Assert.False(ini.IsModified);
			Assert.True(ini.SetValue("A", "key", "5"));

			Assert.True(ini.IsModified);
			Assert.Equal("Key =5", ini.Lines[1]);
		}

		[Fact]
		public void SettingTheSameValueDoesNotModifyTheDocument()
		{
			IniDocument ini = Create("[A]", "Key=1");

			Assert.True(ini.SetValue("A", "Key", "1"));
			Assert.False(ini.IsModified);
		}

		[Fact]
		public void SetValueDoesNotAddOrUncommentByDefault()
		{
			IniDocument ini = Create("[A]", ";Key=1");

			Assert.False(ini.SetValue("A", "Key", "2"));
			Assert.False(ini.SetValue("B", "Key", "2"));
			Assert.Equal(";Key=1", ini.Lines[1]);
			Assert.False(ini.IsModified);
		}

		[Fact]
		public void UpdateCommentedChangesACommentedKeyAndKeepsItCommented()
		{
			IniDocument ini = Create("[A]", ";Key=1");

			Assert.True(ini.SetValue("A", "Key", "2", IniWriteMode.UpdateCommented));
			Assert.Equal(";Key=2", ini.Lines[1]);
		}

		[Fact]
		public void AddIfMissingDoesNotWriteIntoACommentedExample()
		{
			IniDocument ini = Create("[A]", ";Key=1", "Other=2");

			Assert.True(ini.SetValue("A", "Key", "5", IniWriteMode.AddIfMissing));

			Assert.Equal("5", ini.GetValue("A", "Key"));
			Assert.Contains(";Key=1", ini.Lines);
		}

		[Fact]
		public void AddIfMissingUpdatesTheActiveLineEvenWhenACommentedOneComesFirst()
		{
			IniDocument ini = Create("[A]", ";Key=2", "Key=0");

			Assert.True(ini.SetValue("A", "Key", "3", IniWriteMode.AddIfMissing));

			Assert.Equal(["[A]", ";Key=2", "Key=3"], ini.Lines);
		}

		[Fact]
		public void AddIfMissingAppendsAfterTheLastLineOfTheSection()
		{
			IniDocument ini = Create("[A]", "One=1", "", "[B]", "Two=2");

			Assert.True(ini.SetValue("A", "New", "9", IniWriteMode.AddIfMissing));

			Assert.Equal(["[A]", "One=1", String.Empty, "New=9", String.Empty, "[B]", "Two=2"], ini.Lines);
		}

		[Fact]
		public void AddIfMissingCreatesTheSection()
		{
			IniDocument ini = Create("[A]", "One=1");

			Assert.True(ini.SetValue("New", "Key", "7", IniWriteMode.AddIfMissing));

			Assert.Equal(["[A]", "One=1", String.Empty, "[New]", "Key=7"], ini.Lines);
		}

		[Fact]
		public void GlobalSetUpdatesTheFirstMatchingKey()
		{
			IniDocument ini = Create("[A]", "Key=1", "[B]", "Key=2");

			Assert.True(ini.SetValue(null, "Key", "3"));
			Assert.Equal(["[A]", "Key=3", "[B]", "Key=2"], ini.Lines);
		}
	}
}