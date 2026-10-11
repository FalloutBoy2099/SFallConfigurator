namespace SFallConfigurator.Infrastructure
{
	using System;
	using System.ComponentModel;
	using System.IO;
	using Dat;

	/// <summary>
	/// Runs an action that touches files and tells the user when the files cannot be read or written,
	/// instead of letting the error end the program with the unsaved changes.
	/// </summary>
	internal static class ErrorGuard
	{
		/// <summary>
		/// True for the errors that come from the files, the disk or the system, not from a defect of the program.
		/// </summary>
		public static Boolean IsFileError(Exception exception)
		{
			return exception is IOException
				or UnauthorizedAccessException
				or Win32Exception
				or InvalidDataException
				or DatFormatException
				or NotSupportedException
				or OutOfMemoryException
				or OverflowException
				or ArgumentException;
		}

		/// <summary>
		/// Runs the action. Returns false, after showing the error, when a file error stopped it.
		/// </summary>
		public static Boolean Run(IUserInteraction interaction, Action action, String? caption = null)
		{
			try
			{
				action();
				return true;
			}
			catch (Exception exception) when (IsFileError(exception))
			{
				interaction.ShowError(exception.Message, caption);
				return false;
			}
		}
	}
}