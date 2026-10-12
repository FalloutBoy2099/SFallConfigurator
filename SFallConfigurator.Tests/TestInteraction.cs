namespace SFallConfigurator.Tests
{
	using System;
	using System.Collections.Generic;
	using Infrastructure;

	/// <summary>
	/// A user that never chooses anything unless told to, and answers the questions as configured.
	/// What the user would have seen (messages, errors and their captions, the prompts) is recorded.
	/// </summary>
	internal sealed class TestInteraction : IUserInteraction
	{
		public Boolean? SaveAnswer { get; set; } = true;

		/// <summary>
		/// What <see cref="Confirm"/> answers.
		/// </summary>
		public Boolean ConfirmAnswer { get; set; } = true;

		/// <summary>
		/// What PromptText answers: null is a cancelled prompt.
		/// </summary>
		public String? PromptAnswer { get; set; }

		/// <summary>
		/// What PickFile answers: null is a cancelled chooser.
		/// </summary>
		public String? FileAnswer { get; set; }

		/// <summary>
		/// The key code that PickKeyCode returns.
		/// </summary>
		public Int32? KeyToPick { get; set; }

		/// <summary>
		/// How many questions (confirmations and save prompts) were asked.
		/// </summary>
		public Int32 Questions { get; private set; }

		public List<String> Messages { get; } = [];

		public List<String> Errors { get; } = [];

		public List<String?> ErrorCaptions { get; } = [];

		/// <summary>
		/// The text that each prompt started with.
		/// </summary>
		public List<String?> PromptInitialTexts { get; } = [];

		public void ShowMessage(String message, String? caption = null)
		{
			Messages.Add(message);
		}

		public void ShowError(String message, String? caption = null)
		{
			Errors.Add(message);
			ErrorCaptions.Add(caption);
		}

		public Boolean Confirm(String message, String? caption = null)
		{
			Questions++;
			return ConfirmAnswer;
		}

		public Boolean? AskSaveChanges(String message, String? caption = null)
		{
			Questions++;
			return SaveAnswer;
		}

		public Int32? PickKeyCode(Int32 currentCode, Boolean allowModifiers)
		{
			return KeyToPick;
		}

		public String? PromptText(String prompt, String? initialText, String? caption = null)
		{
			PromptInitialTexts.Add(initialText);
			return PromptAnswer;
		}

		public String? PickFile(String filter, String initialDirectory, String? initialFileName = null, Boolean checkFileExists = true)
		{
			return FileAnswer;
		}

		public String? PickFolder(String initialDirectory, String? title = null)
		{
			return null;
		}

		/// <summary>
		/// The index that PickItem answers: null is a cancelled list.
		/// </summary>
		public Int32? ItemToPick { get; set; }

		/// <summary>
		/// The items that the last list offered.
		/// </summary>
		public IReadOnlyList<String> PickedFrom { get; private set; } = [];

		public Int32? PickItem(String prompt, IReadOnlyList<String> items, String? caption = null)
		{
			PickedFrom = items;
			return ItemToPick;
		}

		/// <summary>
		/// The reports that the user was shown, by title.
		/// </summary>
		public List<(String Title, IReadOnlyList<ReportLine> Lines)> Reports { get; } = [];

		public void ShowReport(String title, IReadOnlyList<ReportLine> lines)
		{
			Reports.Add((title, lines));
		}

		/// <summary>
		/// What ReviewChanges answers when the user is asked to save.
		/// </summary>
		public Boolean ReviewAnswer { get; set; } = true;

		/// <summary>
		/// The changes that the user was shown.
		/// </summary>
		public List<ChangePreview> Reviews { get; } = [];

		public Boolean ReviewChanges(ChangePreview preview, Boolean askToSave)
		{
			Reviews.Add(preview);
			return !askToSave || ReviewAnswer;
		}
	}
}