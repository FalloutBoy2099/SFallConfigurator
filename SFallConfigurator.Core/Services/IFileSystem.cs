namespace SFallConfigurator.Services
{
	using System;

	/// <summary>
	/// What the view models ask of the disk. It is a service so that they can be tested without files.
	/// </summary>
	internal interface IFileSystem
	{
		Boolean FileExists(String path);

		Boolean DirectoryExists(String path);
	}

	internal sealed class SystemFileSystem : IFileSystem
	{
		public Boolean FileExists(String path)
		{
			return System.IO.File.Exists(path);
		}

		public Boolean DirectoryExists(String path)
		{
			return System.IO.Directory.Exists(path);
		}
	}
}