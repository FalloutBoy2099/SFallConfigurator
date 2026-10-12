namespace SFallConfigurator.Services
{
	using System;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.IO;

	/// <summary>
	/// Starts other programs: the game, the editor of a file, the browser. A service, so that the view models can be tested
	/// without starting anything.
	/// </summary>
	internal interface IProcessLauncher
	{
		/// <summary>
		/// Starts the game. Throws <see cref="Win32Exception"/> or <see cref="InvalidOperationException"/> when it cannot.
		/// </summary>
		void StartGame(String exePath, String workingDirectory);

		/// <summary>
		/// Opens a file with the program that is associated with it, or with Notepad when there is none.
		/// Throws <see cref="Win32Exception"/> when no program can open it.
		/// </summary>
		void OpenFile(String path);

		/// <summary>
		/// Opens an address in the browser.
		/// </summary>
		void OpenUrl(String url);
	}

	internal sealed class SystemProcessLauncher : IProcessLauncher
	{
		public void StartGame(String exePath, String workingDirectory)
		{
			Process.Start(new ProcessStartInfo(exePath)
			{
				WorkingDirectory = workingDirectory,
				UseShellExecute = true,
			});
		}

		public void OpenFile(String path)
		{
			try
			{
				Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
			}
			catch (Win32Exception)
			{
				// No program is associated with the file type. The Notepad of the system is used, not the one that may sit in
				// the folder of the program, and the path goes in the argument list, which quotes it.
				Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "notepad.exe")) { ArgumentList = { path } });
			}
		}

		public void OpenUrl(String url)
		{
			Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
		}
	}
}