namespace SFallConfigurator.Configuration
{
	using System;
	using System.IO;
	using System.Runtime.InteropServices;
	using System.Text;

	/// <summary>
	/// Text encodings used by the game files. .NET no longer exposes the system ANSI code page
	/// through <see cref="Encoding.Default"/>, but the legacy files were written with it.
	/// </summary>
	internal static partial class TextEncodings
	{
		private static readonly Lazy<Encoding> AnsiEncoding = new Lazy<Encoding>(CreateAnsiEncoding);

		/// <summary>
		/// System ANSI code page (for example 1251 on a Russian system).
		/// </summary>
		public static Encoding Ansi => AnsiEncoding.Value;

		public static Encoding Utf8WithBom { get; } = new UTF8Encoding(true);

		public static Encoding Utf8WithoutBom { get; } = new UTF8Encoding(false);

		/// <summary>
		/// UTF-8 when the file starts with a byte order mark or is valid UTF-8 text, the ANSI code page otherwise.
		/// </summary>
		public static Encoding DetectFileEncoding(String path)
		{
			using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
			Byte[] content = new Byte[stream.Length];
			stream.ReadExactly(content, 0, content.Length);
			return DetectEncoding(content);
		}

		/// <summary>
		/// A file without a byte order mark is read as UTF-8 when it is valid UTF-8 and holds more than ASCII:
		/// an ANSI file with accented letters is almost never valid UTF-8, so the guess is reliable.
		/// </summary>
		internal static Encoding DetectEncoding(ReadOnlySpan<Byte> content)
		{
			if (content.StartsWith(Utf8WithBom.Preamble))
			{
				return Utf8WithBom;
			}

			Boolean hasNonAscii = content.IndexOfAnyExceptInRange((Byte)0, (Byte)0x7F) >= 0;
			return hasNonAscii && System.Text.Unicode.Utf8.IsValid(content) ? Utf8WithoutBom : Ansi;
		}

		private static Encoding CreateAnsiEncoding()
		{
			return CreateAnsiEncoding((Int32)GetACP());
		}

		/// <summary>
		/// The encoding of a code page for the legacy files. These files are read by the game as plain bytes:
		/// an encoding that has a byte order mark (the UTF-8 code page of Windows) must not write it.
		/// </summary>
		internal static Encoding CreateAnsiEncoding(Int32 codePage)
		{
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			Encoding encoding = Encoding.GetEncoding(codePage);
			return encoding.GetPreamble().Length == 0 ? encoding : new UTF8Encoding(false);
		}

		[LibraryImport("kernel32.dll")]
		private static partial UInt32 GetACP();
	}
}