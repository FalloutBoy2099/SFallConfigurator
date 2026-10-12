namespace SFallConfigurator.Configuration
{
	using System;
	using System.IO;

	internal static class PathHelper
	{
		/// <summary>
		/// Directory that holds the executable. This is the game folder the tool is installed in.
		/// </summary>
		public static String GetApplicationDirectory()
		{
			String? processPath = Environment.ProcessPath;
			String? directory = processPath is null ? null : Path.GetDirectoryName(processPath);
			return directory ?? AppContext.BaseDirectory.TrimEnd('\\', '/');
		}

		/// <summary>
		/// Resolves a path stored in a game file: rooted paths are kept, others are relative to the game folder.
		/// </summary>
		public static String Resolve(String gamePath, String path)
		{
			if (Path.IsPathFullyQualified(path))
			{
				return path;
			}

			return Path.Combine(gamePath, path.TrimStart('\\', '/'));
		}

		/// <summary>
		/// Converts a chosen path to the lower case, game relative form used by the game configuration files.
		/// </summary>
		public static String ToGameRelative(String gamePath, String fullPath, Boolean trailingSeparator = false)
		{
			String relative = Path.GetRelativePath(gamePath, fullPath).TrimEnd('\\', '/');
			if (trailingSeparator)
			{
				relative += Path.DirectorySeparatorChar;
			}

			return relative.ToLowerInvariant();
		}
	}
}