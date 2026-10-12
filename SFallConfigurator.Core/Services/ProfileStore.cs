namespace SFallConfigurator.Services
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using Configuration;

	/// <summary>
	/// A named set of option values that the user keeps to apply again later. The keys say which option a value belongs to.
	/// </summary>
	internal sealed record Profile(String Name, IReadOnlyDictionary<String, String> Values);

	/// <summary>
	/// The profiles of the user: one small text file each, in the folder of the settings of the program.
	/// </summary>
	internal sealed class ProfileStore
	{
		private const String Extension = ".profile";

		private readonly String _folder;

		public ProfileStore(String folder)
		{
			_folder = folder;
		}

		/// <summary>
		/// The names of the profiles, in alphabetical order.
		/// </summary>
		public IReadOnlyList<String> GetNames()
		{
			if (!Directory.Exists(_folder))
			{
				return [];
			}

			return Directory.GetFiles(_folder, "*" + Extension)
				.Select(file => Path.GetFileNameWithoutExtension(file))
				.Order(StringComparer.CurrentCultureIgnoreCase)
				.ToList();
		}

		public Boolean Exists(String name)
		{
			return File.Exists(GetPath(name));
		}

		public Profile? Load(String name)
		{
			String path = GetPath(name);
			if (!File.Exists(path))
			{
				return null;
			}

			Dictionary<String, String> values = new(StringComparer.OrdinalIgnoreCase);
			foreach (String line in File.ReadAllLines(path, Encoding.UTF8))
			{
				if (IniDocument.TryParseEntry(line, out String key, out String value))
				{
					values[key] = value;
				}
			}

			return new Profile(name, values);
		}

		public void Save(Profile profile)
		{
			Directory.CreateDirectory(_folder);
			List<String> lines = ["; SFallConfigurator profile"];
			lines.AddRange(profile.Values.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => pair.Key + "=" + pair.Value));
			StagedFile.WriteAllLines(GetPath(profile.Name), lines, new UTF8Encoding(false));
		}

		public void Delete(String name)
		{
			String path = GetPath(name);
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}

		/// <summary>
		/// A name that can be the name of a file: the characters that Windows forbids are replaced.
		/// </summary>
		public static String CleanName(String name)
		{
			Char[] invalid = Path.GetInvalidFileNameChars();
			return new String(name.Trim().Select(character => invalid.Contains(character) ? '_' : character).ToArray()).TrimEnd('.', ' ');
		}

		private String GetPath(String name)
		{
			return Path.Combine(_folder, CleanName(name) + Extension);
		}
	}
}