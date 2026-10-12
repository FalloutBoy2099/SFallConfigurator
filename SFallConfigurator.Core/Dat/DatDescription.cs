namespace SFallConfigurator.Dat
{
	using System;
	using System.IO;
	using System.Text;
	using Localization;

	/// <summary>
	/// The optional "desc.id" file of a DAT: the file name on the first line, a description on the second.
	/// </summary>
	internal static class DatDescription
	{
		private const String DescriptionFileName = "desc.id";
		private const Int32 MaximumDescriptionFileSize = 1024 * 1024;

		/// <summary>
		/// Reads the description of a DAT. Returns null when the archive has none.
		/// Throws <see cref="DatFormatException"/> or <see cref="IOException"/> when the archive cannot be read.
		/// </summary>
		public static String? Read(String datPath)
		{
			using DatArchive archive = DatArchive.Open(datPath, writable: false);

			DatEntry? entry = archive.FindFile(DescriptionFileName);
			if (entry is null)
			{
				return null;
			}

			// A description is a line of text: an entry larger than that is damaged, or made to use the memory.
			if (entry.UnpackedSize is < 0 or > MaximumDescriptionFileSize)
			{
				throw new DatFormatException(Localizer.T("The DAT directory is damaged."));
			}

			String[] lines = Encoding.UTF8.GetString(archive.ReadFile(entry)).Split('\n');
			String? description = lines.Length > 1 ? lines[1].TrimEnd() : null;
			return String.IsNullOrEmpty(description) ? null : description;
		}

		public static void Write(String datPath, String description)
		{
			using DatArchive archive = DatArchive.Open(datPath, writable: true);

			String content = Path.GetFileName(datPath) + "\r\n" + description;
			archive.SetFile(DescriptionFileName, Encoding.UTF8.GetBytes(content));
			archive.Save();
		}
	}
}