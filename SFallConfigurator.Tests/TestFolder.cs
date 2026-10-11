namespace SFallConfigurator.Tests
{
	using System;
	using System.IO;

	/// <summary>
	/// A temporary folder that is deleted when the test is done.
	/// </summary>
	internal sealed class TestFolder : IDisposable
	{
		public TestFolder()
		{
			Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SFallConfiguratorTests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path);
		}

		public String Path { get; }

		public String Combine(String relativePath)
		{
			return System.IO.Path.Combine(Path, relativePath);
		}

		public String Write(String relativePath, String content, System.Text.Encoding? encoding = null)
		{
			String full = Combine(relativePath);
			Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
			File.WriteAllText(full, content, encoding ?? new System.Text.UTF8Encoding(false));
			return full;
		}

		public void Dispose()
		{
			try
			{
				foreach (String file in Directory.EnumerateFiles(Path, "*", SearchOption.AllDirectories))
				{
					File.SetAttributes(file, FileAttributes.Normal);
				}

				Directory.Delete(Path, recursive: true);
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				// A leftover temporary folder is harmless.
			}
		}
	}
}