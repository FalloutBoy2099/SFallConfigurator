namespace SFallConfigurator.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;

	/// <summary>
	/// How <see cref="IniDocument.SetValue"/> treats keys that are not found as active entries.
	/// </summary>
	internal enum IniWriteMode
	{
		/// <summary>
		/// Only an existing, uncommented key is updated.
		/// </summary>
		UpdateExisting,

		/// <summary>
		/// An existing key is updated even when its line is commented out (the comment is kept).
		/// </summary>
		UpdateCommented,

		/// <summary>
		/// An existing, uncommented key is updated; a missing key (and its section) is appended.
		/// A commented line is not the key: the key is appended and the comment stays as it is.
		/// </summary>
		AddIfMissing,
	}

	/// <summary>
	/// An ini file kept as a list of lines, so unrelated content (comments, spacing, order)
	/// is written back untouched.
	/// </summary>
	internal sealed class IniDocument
	{
		private static readonly Char[] CommentPrefixChars = [';', ' '];

		private readonly List<String> _lines;

		private IReadOnlyList<String> _savedLines;

		public IniDocument(String path, IEnumerable<String> lines, Encoding writeEncoding)
		{
			Path = path;
			WriteEncoding = writeEncoding;
			_lines = new List<String>(lines);
			_savedLines = _lines.ToArray();
		}

		public String Path { get; }

		public Encoding WriteEncoding { get; }

		/// <summary>
		/// The line break the file used when it was read, written again so that a file is not changed as a whole.
		/// </summary>
		public String NewLine { get; init; } = Environment.NewLine;

		/// <summary>
		/// Whether the last line of the file was ended by a line break.
		/// </summary>
		public Boolean HasFinalNewline { get; init; } = true;

		/// <summary>
		/// True when the content differs from what was loaded or last saved.
		/// </summary>
		public Boolean IsModified { get; private set; }

		public Int32 LineCount => _lines.Count;

		public IReadOnlyList<String> Lines => _lines;

		/// <summary>
		/// The lines as they were when the file was read or last saved.
		/// </summary>
		public IReadOnlyList<String> SavedLines => _savedLines;

		public static IniDocument Load(String path, Encoding readEncoding, Encoding writeEncoding)
		{
			String text;
			using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			using (StreamReader reader = new StreamReader(stream, readEncoding, detectEncodingFromByteOrderMarks: true))
			{
				text = reader.ReadToEnd();
			}

			String newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n"
				: text.Contains('\n') ? "\n"
				: text.Contains('\r') ? "\r"
				: Environment.NewLine;
			Boolean finalNewline = text.Length == 0 || text.EndsWith('\n') || text.EndsWith('\r');

			List<String> lines = [.. text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None)];
			if (finalNewline && lines.Count > 0 && lines[^1].Length == 0)
			{
				lines.RemoveAt(lines.Count - 1);
			}

			return new IniDocument(path, lines, writeEncoding) { NewLine = newLine, HasFinalNewline = finalNewline };
		}

		/// <summary>
		/// Loads a file and writes it back in the encoding it had: with or without a byte order mark, in UTF-8 or in
		/// the ANSI code page. A program that reads the file as ANSI must not find a different encoding after a save.
		/// </summary>
		public static IniDocument LoadDetected(String path)
		{
			Encoding encoding = TextEncodings.DetectFileEncoding(path);
			return Load(path, encoding, encoding);
		}

		public static IniDocument CreateEmpty(String path, Encoding writeEncoding)
		{
			return new IniDocument(path, [], writeEncoding);
		}

		public void Save()
		{
			Stage().Commit();
		}

		/// <summary>
		/// Writes the content next to the file. The file itself is replaced when the result is committed,
		/// so that several documents can be saved together or not at all.
		/// </summary>
		public StagedFile Stage()
		{
			String text = String.Join(NewLine, _lines) + (HasFinalNewline && _lines.Count > 0 ? NewLine : String.Empty);
			String[] staged = _lines.ToArray();
			return StagedFile.WriteText(Path, text, WriteEncoding, () =>
			{
				IsModified = false;
				_savedLines = staged;
			});
		}

		public void MarkModified()
		{
			IsModified = true;
		}

		public Boolean HasSection(String section)
		{
			return FindSection(section) >= 0;
		}

		/// <summary>
		/// Looks a key up. With a null section the first matching key of the whole file wins.
		/// </summary>
		public Boolean TryGetValue(String? section, String key, out String value)
		{
			value = String.Empty;

			Int32 start = 0;
			if (section is not null)
			{
				Int32 sectionIndex = FindSection(section);
				if (sectionIndex < 0)
				{
					return false;
				}

				start = sectionIndex + 1;
			}

			for (Int32 index = start; index < _lines.Count; index++)
			{
				String line = _lines[index];
				if (section is not null && TryParseSection(line, out _))
				{
					break;
				}

				if (TryParseEntry(line, out String name, out String entryValue) && name.Equals(key, StringComparison.OrdinalIgnoreCase))
				{
					value = entryValue;
					return true;
				}
			}

			return false;
		}

		public String? GetValue(String? section, String key)
		{
			return TryGetValue(section, key, out String value) ? value : null;
		}

		/// <summary>
		/// Writes a value. Returns false when nothing could be written (see <see cref="IniWriteMode"/>).
		/// </summary>
		public Boolean SetValue(String? section, String key, String value, IniWriteMode mode = IniWriteMode.UpdateExisting)
		{
			Int32 start = 0;
			Int32 lastContentLine = -1;

			if (section is not null)
			{
				Int32 sectionIndex = FindSection(section);
				if (sectionIndex < 0)
				{
					if (mode != IniWriteMode.AddIfMissing)
					{
						return false;
					}

					AppendSection(NormalizeSection(section), key, value);
					return true;
				}

				start = sectionIndex + 1;
				lastContentLine = sectionIndex;
			}

			for (Int32 index = start; index < _lines.Count; index++)
			{
				String line = _lines[index];
				if (section is not null && TryParseSection(line, out _))
				{
					break;
				}

				Int32 equalsIndex = line.IndexOf('=');
				if (equalsIndex > 0)
				{
					String name = line.Substring(0, equalsIndex).Trim();
					if (mode == IniWriteMode.UpdateCommented)
					{
						name = name.TrimStart(CommentPrefixChars);
					}

					if (name.Equals(key, StringComparison.OrdinalIgnoreCase))
					{
						ReplaceLine(index, line.Substring(0, equalsIndex + 1) + value);
						return true;
					}
				}

				if (!String.IsNullOrWhiteSpace(line))
				{
					lastContentLine = index;
				}
			}

			if (mode == IniWriteMode.AddIfMissing && section is not null)
			{
				// A blank line separates the new key from the previous content of the section.
				_lines.Insert(lastContentLine + 1, key + "=" + value);
				_lines.Insert(lastContentLine + 1, String.Empty);
				IsModified = true;
				return true;
			}

			return false;
		}

		public void InsertLines(Int32 index, IEnumerable<String> lines)
		{
			_lines.InsertRange(index, lines);
			IsModified = true;
		}

		/// <summary>
		/// Index of the line holding the "[section]" header, or -1.
		/// </summary>
		public Int32 FindSection(String section)
		{
			String wanted = NormalizeSection(section);
			for (Int32 index = 0; index < _lines.Count; index++)
			{
				if (TryParseSection(_lines[index], out String name) && name.Equals(wanted, StringComparison.OrdinalIgnoreCase))
				{
					return index;
				}
			}

			return -1;
		}

		/// <summary>
		/// Splits "key=value". Commented lines and lines without a key are not entries.
		/// </summary>
		public static Boolean TryParseEntry(String line, out String key, out String value)
		{
			key = String.Empty;
			value = String.Empty;

			String trimmed = line.TrimStart();
			if (trimmed.Length == 0 || trimmed[0] == ';' || trimmed[0] == '[')
			{
				return false;
			}

			Int32 equalsIndex = line.IndexOf('=');
			if (equalsIndex <= 0)
			{
				return false;
			}

			key = line.Substring(0, equalsIndex).Trim();
			value = line.Substring(equalsIndex + 1).Trim();
			return key.Length > 0;
		}

		public static Boolean TryParseSection(String line, out String name)
		{
			name = String.Empty;

			String trimmed = line.TrimStart();
			if (trimmed.Length == 0 || trimmed[0] != '[')
			{
				return false;
			}

			Int32 closing = trimmed.IndexOf(']');
			if (closing < 2)
			{
				return false;
			}

			name = trimmed.Substring(1, closing - 1).Trim();
			return true;
		}

		public static String NormalizeSection(String section)
		{
			return section.Trim().TrimStart('[').TrimEnd(']').Trim();
		}

		private void ReplaceLine(Int32 index, String newLine)
		{
			if (_lines[index] == newLine)
			{
				return;
			}

			_lines[index] = newLine;
			IsModified = true;
		}

		private void AppendSection(String section, String key, String value)
		{
			if (_lines.Count > 0 && !String.IsNullOrWhiteSpace(_lines[^1]))
			{
				_lines.Add(String.Empty);
			}

			_lines.Add("[" + section + "]");
			_lines.Add(key + "=" + value);
			IsModified = true;
		}
	}
}