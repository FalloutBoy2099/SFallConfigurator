namespace SFallConfigurator.Services
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using Configuration;
	using Dat;
	using Infrastructure;

	/// <summary>
	/// The 256 colors of the game (color.pal), which the Hi-Res patch uses to name a color by its number. The file is looked for
	/// in the patch folder, in the data folder of the game and at the root of master.dat.
	/// </summary>
	internal static class GamePalette
	{
		private const String FileName = "color.pal";
		private const Int32 ColorCount = 256;

		/// <summary>
		/// The colors as 0xRRGGBB, or null when the game has no readable palette.
		/// </summary>
		public static IReadOnlyList<Int32>? Load(GameConfiguration configuration)
		{
			return ErrorGuard.Try(() => Find(configuration), null);
		}

		/// <summary>
		/// Reads a palette: 256 colors of three bytes, each from 0 to 63.
		/// </summary>
		public static IReadOnlyList<Int32>? Parse(Byte[] data)
		{
			if (data.Length < ColorCount * 3)
			{
				return null;
			}

			Int32[] colors = new Int32[ColorCount];
			for (Int32 index = 0; index < ColorCount; index++)
			{
				Int32 red = Scale(data[index * 3]);
				Int32 green = Scale(data[(index * 3) + 1]);
				Int32 blue = Scale(data[(index * 3) + 2]);
				colors[index] = (red << 16) | (green << 8) | blue;
			}

			return colors;
		}

		private static Int32 Scale(Byte value)
		{
			return Math.Min(value, (Byte)63) * 255 / 63;
		}

		private static IReadOnlyList<Int32>? Find(GameConfiguration configuration)
		{
			String gamePath = configuration.GamePath;
			List<String> folders = [Path.Combine(gamePath, "data")];
			if (configuration.GameCfg.GetValue("system", "master_patches") is { Length: > 0 } patches)
			{
				folders.Insert(0, PathHelper.Resolve(gamePath, patches));
			}

			foreach (String folder in folders)
			{
				String file = Path.Combine(folder, FileName);
				if (File.Exists(file) && Parse(File.ReadAllBytes(file)) is { } fromFile)
				{
					return fromFile;
				}
			}

			if (configuration.GameCfg.GetValue("system", "master_dat") is { Length: > 0 } master && File.Exists(PathHelper.Resolve(gamePath, master)))
			{
				using DatArchive archive = DatArchive.Open(PathHelper.Resolve(gamePath, master), writable: false);
				if (archive.FindFile(FileName) is { } entry)
				{
					return Parse(archive.ReadFile(entry));
				}
			}

			return null;
		}
	}
}