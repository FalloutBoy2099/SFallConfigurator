namespace SFallConfigurator.Tests
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using Xunit;

	/// <summary>
	/// The rules of the style of the sources that the analyzers of the compiler cannot check: the format of the files, the names of
	/// the types, the place of the usings, the language of the comments.
	/// </summary>
	public sealed partial class SourceStyleTests
	{
		private static readonly String[] Projects = ["SFallConfigurator.Core", "SFallConfigurator.ViewModels", "SFallConfigurator", "SFallConfigurator.Tests"];

		[GeneratedRegex(@"(?<![\w.@])(int|uint|string|bool|object|char|byte|sbyte|short|ushort|long|ulong|double|float|decimal)(?![\w])")]
		private static partial Regex KeywordType();

		[GeneratedRegex(@"(?<![\w.@])var\s+\w+")]
		private static partial Regex VarDeclaration();

		[GeneratedRegex(@"[À-ɏ]")]
		private static partial Regex AccentedLetter();

		private static String? FindRoot()
		{
			String? directory = AppContext.BaseDirectory;
			while (directory is not null && !File.Exists(Path.Combine(directory, "SFallConfigurator.slnx")))
			{
				directory = Path.GetDirectoryName(directory);
			}

			return directory;
		}

		private static IEnumerable<(String Relative, String Text)> ReadSources(String extension)
		{
			String? root = FindRoot();
			if (root is null)
			{
				yield break;
			}

			foreach (String project in Projects)
			{
				String folder = Path.Combine(root, project);
				if (!Directory.Exists(folder))
				{
					continue;
				}

				foreach (String file in Directory.EnumerateFiles(folder, "*" + extension, SearchOption.AllDirectories))
				{
					String relative = Path.GetRelativePath(root, file);
					if (relative.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
						|| relative.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
						|| relative.EndsWith(".g.cs", StringComparison.Ordinal)
						|| relative.EndsWith(".g.i.cs", StringComparison.Ordinal))
					{
						continue;
					}

					yield return (relative, File.ReadAllText(file));
				}
			}
		}

		/// <summary>
		/// The code of a source without its comments and its strings, line by line, so that a word in a text is not taken for code.
		/// </summary>
		private static IEnumerable<(Int32 Number, String Code)> GetCodeLines(String text)
		{
			String withoutBlocks = Regex.Replace(text, @"/\*.*?\*/", match => Regex.Replace(match.Value, @"[^\r\n]", " "), RegexOptions.Singleline);
			Int32 number = 0;
			foreach (String line in withoutBlocks.Split('\n'))
			{
				number++;
				String code = Regex.Replace(line.TrimEnd('\r'), @"""(?:[^""\\]|\\.)*""", "\"\"");
				code = Regex.Replace(code, @"'(?:[^'\\]|\\.)*'", "''");
				yield return (number, Regex.Replace(code, @"//.*$", String.Empty));
			}
		}

		private static void AssertNoProblem(List<String> problems)
		{
			Assert.True(problems.Count == 0, Environment.NewLine + String.Join(Environment.NewLine, problems));
		}

		[Fact]
		public void TheFilesHaveWindowsLineBreaksTabsAndNoFinalLineBreak()
		{
			List<String> problems = [];
			foreach ((String relative, String text) in ReadSources(".cs"))
			{
				if (Regex.IsMatch(text, @"(?<!\r)\n"))
				{
					problems.Add(relative + ": a line break is not CRLF");
				}

				if (text.EndsWith('\n'))
				{
					problems.Add(relative + ": the file ends with a line break");
				}

				Int32 number = 0;
				foreach (String line in text.Split('\n'))
				{
					number++;
					if (Regex.IsMatch(line, @"^ +\S") && !line.TrimStart().StartsWith('*'))
					{
						problems.Add(relative + ":" + number + ": the indentation is made of spaces");
						break;
					}
				}
			}

			AssertNoProblem(problems);
		}

		[Fact]
		public void TheTypesAreWrittenWithTheirNamesOfTheRuntime()
		{
			List<String> problems = [];
			foreach ((String relative, String text) in ReadSources(".cs"))
			{
				foreach ((Int32 number, String code) in GetCodeLines(text))
				{
					foreach (Match match in KeywordType().Matches(code))
					{
						problems.Add(relative + ":" + number + ": " + match.Value + " (use the name of the type of the runtime)");
					}

					if (VarDeclaration().IsMatch(code))
					{
						problems.Add(relative + ":" + number + ": var (write the type)");
					}
				}
			}

			AssertNoProblem(problems);
		}

		[Fact]
		public void TheUsingsAreInsideABlockScopedNamespace()
		{
			List<String> problems = [];
			foreach ((String relative, String text) in ReadSources(".cs"))
			{
				Match? ns = Regex.Match(text, @"^namespace\b", RegexOptions.Multiline);
				Match? use = Regex.Match(text, @"^using\s+[\w.]+;", RegexOptions.Multiline);
				if (ns.Success && use.Success && use.Index < ns.Index)
				{
					problems.Add(relative + ": a using is outside of the namespace");
				}

				if (Regex.IsMatch(text, @"^namespace [\w.]+;", RegexOptions.Multiline))
				{
					problems.Add(relative + ": the namespace is file-scoped");
				}
			}

			AssertNoProblem(problems);
		}

		[Fact]
		public void TheCommentsAreInEnglish()
		{
			List<String> problems = [];
			foreach ((String relative, String text) in ReadSources(".cs"))
			{
				Int32 number = 0;
				foreach (String line in text.Split('\n'))
				{
					number++;
					Int32 comment = line.IndexOf("//", StringComparison.Ordinal);
					if (comment >= 0 && !line[..comment].Contains('"') && AccentedLetter().IsMatch(line[comment..]))
					{
						problems.Add(relative + ":" + number + ": " + line.Trim());
					}
				}
			}

			AssertNoProblem(problems);
		}

		[Fact]
		public void TheXamlFilesAreIndentedWithFourSpaces()
		{
			List<String> problems = [];
			foreach ((String relative, String text) in ReadSources(".xaml"))
			{
				if (text.Contains('\t'))
				{
					problems.Add(relative + ": the XAML file has a tab");
				}
			}

			AssertNoProblem(problems);
		}
	}
}