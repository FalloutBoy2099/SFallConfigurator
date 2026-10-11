namespace SFallConfigurator.Mods
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.IO;
	using System.Linq;
	using Configuration;

	/// <summary>
	/// Manages the load order of the mods in the "mods" folder (mods_order.ini).
	/// <para>
	/// The file is kept as lines: comments and unknown lines stay where they are, and each mod owns a
	/// "slot" (a null entry in the line list). Moving a mod therefore only reorders the mods, not the comments.
	/// </para>
	/// </summary>
	internal sealed class ModsManager
	{
		private static readonly Char[] CommentChars = [';', '#'];

		private readonly String _modsDirectory;
		private readonly String _orderIniPath;
		private readonly String _orderTxtPath;

		private List<String?> _lines = [];
		private Boolean _isModified;

		public ModsManager(String gamePath)
		{
			_modsDirectory = Path.Combine(gamePath, "mods");
			_orderIniPath = Path.Combine(_modsDirectory, "mods_order.ini");
			_orderTxtPath = Path.Combine(_modsDirectory, "mods_order.txt");
		}

		/// <summary>
		/// Mods in load order. The last ones win over the first ones.
		/// </summary>
		public ObservableCollection<ModEntry> Mods { get; } = [];

		/// <summary>
		/// Mods found in the mods folder that are not in the load order (they load automatically).
		/// </summary>
		public ObservableCollection<String> AvailableMods { get; } = [];

		public Boolean OrderFileExists { get; private set; }

		public String OrderFilePath => _orderIniPath;

		/// <summary>
		/// Relative path of the order file, used to open it in an editor.
		/// </summary>
		public String OrderFileRelativePath => Path.Combine("mods", "mods_order.ini");

		public Boolean IsModified => _isModified;

		public void Load()
		{
			Mods.Clear();
			_lines = [];
			_isModified = false;
			OrderFileExists = File.Exists(_orderIniPath);

			if (OrderFileExists)
			{
				foreach (String line in File.ReadAllLines(_orderIniPath, TextEncodings.Ansi))
				{
					ModEntry? mod = ParseOrderLine(line);
					if (mod is null)
					{
						_lines.Add(line);
					}
					else
					{
						_lines.Add(null);
						Track(mod);
						Mods.Add(mod);
					}
				}
			}

			RefreshAvailableMods();
		}

		/// <summary>
		/// Writes mods_order.ini when the order or a state was changed.
		/// </summary>
		public void Save()
		{
			Stage()?.Commit();
		}

		/// <summary>
		/// Writes the new mods_order.ini next to the old one, to be committed with the other files. Null when nothing changed.
		/// </summary>
		public StagedFile? Stage()
		{
			if (!IsModified || !OrderFileExists)
			{
				return null;
			}

			List<String> output = new(_lines.Count);
			Int32 slot = 0;
			foreach (String? line in _lines)
			{
				output.Add(line ?? FormatOrderLine(Mods[slot++]));
			}

			return StagedFile.Write(_orderIniPath, output, TextEncodings.Ansi, () => _isModified = false);
		}

		/// <summary>
		/// Creates mods_order.ini from mods_order.txt, or from every mod found when there is no text file.
		/// </summary>
		public void CreateOrderFile()
		{
			Mods.Clear();
			_lines = [];

			if (File.Exists(_orderTxtPath))
			{
				foreach (String line in File.ReadAllLines(_orderTxtPath, TextEncodings.Ansi))
				{
					ModEntry? mod = ParseTextLine(line);
					if (mod is null)
					{
						_lines.Add(line);
					}
					else
					{
						_lines.Add(null);
						Track(mod);
						Mods.Add(mod);
					}
				}
			}
			else
			{
				foreach (String name in AvailableMods)
				{
					ModEntry mod = new(name, isEnabled: true, isMissing: false);
					_lines.Add(null);
					Track(mod);
					Mods.Add(mod);
				}
			}

			OrderFileExists = true;
			_isModified = true;
			Save();
			RefreshAvailableMods();
		}

		/// <summary>
		/// Moves a mod from the available list into the load order.
		/// </summary>
		public void Add(Int32 index, String name)
		{
			if (!OrderFileExists)
			{
				OrderFileExists = true;
				_lines = [];
			}

			index = Math.Clamp(index, 0, Mods.Count);

			ModEntry mod = new(name, isEnabled: true, isMissing: false);
			Int32 lineIndex = index < Mods.Count ? FindSlotLine(index) : _lines.Count;
			_lines.Insert(lineIndex, null);
			Track(mod);
			Mods.Insert(index, mod);
			AvailableMods.Remove(name);
			_isModified = true;
		}

		/// <summary>
		/// Takes a mod out of the load order. It becomes available again when it exists.
		/// </summary>
		public void Remove(Int32 index)
		{
			ModEntry mod = Mods[index];
			_lines.RemoveAt(FindSlotLine(index));
			Mods.RemoveAt(index);
			_isModified = true;

			if (!mod.IsMissing)
			{
				InsertAvailable(mod.Name);
			}
		}

		public void Move(Int32 fromIndex, Int32 toIndex)
		{
			if (fromIndex == toIndex)
			{
				return;
			}

			Mods.Move(fromIndex, toIndex);
			_isModified = true;
		}

		private void RefreshAvailableMods()
		{
			AvailableMods.Clear();
			if (!Directory.Exists(_modsDirectory))
			{
				return;
			}

			IEnumerable<String> names = Directory.GetFiles(_modsDirectory, "*.dat", SearchOption.TopDirectoryOnly)
				.Concat(Directory.GetDirectories(_modsDirectory, "*", SearchOption.TopDirectoryOnly))
				.Select(path => Path.GetFileName(path))
				.OrderBy(name => name, StringComparer.CurrentCulture);

			foreach (String name in names)
			{
				if (!Mods.Any(mod => mod.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
				{
					AvailableMods.Add(name);
				}
			}
		}

		private void InsertAvailable(String name)
		{
			Int32 position = 0;
			while (position < AvailableMods.Count && StringComparer.CurrentCulture.Compare(AvailableMods[position], name) < 0)
			{
				position++;
			}

			AvailableMods.Insert(position, name);
		}

		private Int32 FindSlotLine(Int32 slot)
		{
			Int32 seen = -1;
			for (Int32 index = 0; index < _lines.Count; index++)
			{
				if (_lines[index] is null && ++seen == slot)
				{
					return index;
				}
			}

			throw new ArgumentOutOfRangeException(nameof(slot));
		}

		private void Track(ModEntry mod)
		{
			mod.PropertyChanged += (_, _) => _isModified = true;
		}

		private Boolean ModExists(String name)
		{
			String path = Path.Combine(_modsDirectory, name);
			return File.Exists(path) || Directory.Exists(path);
		}

		/// <summary>
		/// Parses "name=1" (enabled) or "name=0" (disabled). Returns null for comments and blank lines.
		/// </summary>
		private ModEntry? ParseOrderLine(String line)
		{
			SplitComment(line, out String content, out String comment);

			Int32 equalsIndex = content.IndexOf('=');
			if (equalsIndex < 0)
			{
				return null;
			}

			String name = content.Substring(0, equalsIndex).Trim();
			if (name.Length == 0)
			{
				return null;
			}

			Boolean isEnabled = content.Substring(equalsIndex + 1).Trim() != "0";
			return new ModEntry(name, isEnabled, !ModExists(name), comment);
		}

		/// <summary>
		/// Parses a line of mods_order.txt, which holds only the mod name.
		/// </summary>
		private ModEntry? ParseTextLine(String line)
		{
			SplitComment(line, out String content, out String comment);

			String name = content.Trim();
			return name.Length == 0 ? null : new ModEntry(name, isEnabled: true, !ModExists(name), comment);
		}

		private static void SplitComment(String line, out String content, out String comment)
		{
			Int32 commentIndex = line.IndexOfAny(CommentChars);
			content = commentIndex < 0 ? line : line.Substring(0, commentIndex);
			comment = commentIndex < 0 ? String.Empty : line.Substring(commentIndex);
		}

		private static String FormatOrderLine(ModEntry mod)
		{
			String line = mod.Name + "=" + (mod.IsEnabled ? "1" : "0");
			return mod.Comment.Length == 0 ? line : line + " " + mod.Comment;
		}
	}
}