namespace SFallConfigurator.Infrastructure
{
	using System;

	/// <summary>
	/// What the main view model asks of the application that owns the window.
	/// </summary>
	internal interface IAppHost
	{
		/// <summary>
		/// Closes the window, and so ends the program.
		/// </summary>
		void Close();

		/// <summary>
		/// Shows another game folder, after the pending changes were dealt with.
		/// </summary>
		void ChangeGameFolder(String gamePath);

		/// <summary>
		/// Reads the game folder again, without asking about the changes that were not saved (they are dropped): the files
		/// were replaced or changed by another program.
		/// </summary>
		/// <param name="notice">A sentence to show to the user once the folder is read again, or null.</param>
		void Reload(String? notice = null);

		/// <summary>
		/// Switches the language of the interface; the window is rebuilt.
		/// </summary>
		void ChangeLanguage(String code);
	}
}