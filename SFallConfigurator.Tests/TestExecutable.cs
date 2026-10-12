namespace SFallConfigurator.Tests
{
	using System;
	using System.IO;
	using System.Text;

	/// <summary>
	/// A file that looks like the Fallout 2 executable for the patcher: its signature and the name of the library.
	/// </summary>
	internal static class TestExecutable
	{
		public static String Create(TestFolder folder, Boolean valid = true)
		{
			Byte[] image = new Byte[1100000];
			if (valid)
			{
				Encoding.ASCII.GetBytes("GNW95MUTEX").CopyTo(image, 1040600);
				Encoding.ASCII.GetBytes("DDRAW").CopyTo(image, 0x000FE150);
			}

			String path = folder.Combine(valid ? "fallout2.exe" : "other.exe");
			File.WriteAllBytes(path, image);
			return path;
		}
	}
}