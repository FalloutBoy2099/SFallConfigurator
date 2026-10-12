namespace SFallConfigurator.Infrastructure
{
	using System;
	using System.Globalization;
	using System.IO;
	using Configuration;

	/// <summary>
	/// Keeps the details of the errors that the program did not expect, so that they can be diagnosed:
	/// the dialog shows the message only.
	/// </summary>
	internal static class ErrorLog
	{
		private const String FileName = "error.log";

		/// <summary>
		/// Appends the error, with its stack, to error.log in the settings folder. Failing to write is not an error.
		/// </summary>
		public static void Write(Exception exception)
		{
			try
			{
				String directory = UserSettings.SettingsDirectory;
				Directory.CreateDirectory(directory);
				File.AppendAllText(Path.Combine(directory, FileName), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + Environment.NewLine + exception + Environment.NewLine + Environment.NewLine);
			}
			catch (Exception logging) when (logging is IOException or UnauthorizedAccessException)
			{
				// There is nowhere to report a failure of the log itself.
			}
		}
	}
}