namespace SFallConfigurator.Infrastructure
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Everything the view models need from the user that is not plain data binding.
	/// </summary>
	internal interface IUserInteraction
	{
		void ShowMessage(String message, String? caption = null);

		void ShowError(String message, String? caption = null);

		Boolean Confirm(String message, String? caption = null);

		/// <summary>
		/// Asks whether pending changes must be kept. Returns null when the user cancels.
		/// </summary>
		Boolean? AskSaveChanges(String message, String? caption = null);

		/// <summary>
		/// Lets the user choose a DirectInput key code. Returns null when cancelled.
		/// </summary>
		Int32? PickKeyCode(Int32 currentCode, Boolean allowModifiers);

		/// <summary>
		/// Asks for a line of text. Returns null when cancelled.
		/// </summary>
		String? PromptText(String prompt, String? initialText, String? caption = null);

		/// <summary>
		/// Opens a file chooser. The filter uses the Windows "name|pattern" syntax.
		/// </summary>
		String? PickFile(String filter, String initialDirectory, String? initialFileName = null, Boolean checkFileExists = true);

		String? PickFolder(String initialDirectory, String? title = null);

		/// <summary>
		/// Lets the user choose one of the items. Returns its index, or null when cancelled.
		/// </summary>
		Int32? PickItem(String prompt, IReadOnlyList<String> items, String? caption = null);

		/// <summary>
		/// Shows a report: a title, and lines that are marked by how serious they are.
		/// </summary>
		void ShowReport(String title, IReadOnlyList<ReportLine> lines);

		/// <summary>
		/// Shows what saving would change. With <paramref name="askToSave"/> the user can refuse (the result is then false).
		/// </summary>
		Boolean ReviewChanges(ChangePreview preview, Boolean askToSave);
	}
}