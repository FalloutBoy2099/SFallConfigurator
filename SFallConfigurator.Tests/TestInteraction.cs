namespace SFallConfigurator.Tests
{
	using System;
	using System.Collections.Generic;
	using Infrastructure;

	/// <summary>
	/// A user that never chooses anything and answers the questions as configured. The errors shown are recorded.
	/// </summary>
	internal sealed class TestInteraction : IUserInteraction
	{
		public Boolean? SaveAnswer { get; set; } = true;

		/// <summary>
		/// The key code that PickKeyCode returns.
		/// </summary>
		public Int32? KeyToPick { get; set; }

		/// <summary>
		/// How many questions (confirmations and save prompts) were asked.
		/// </summary>
		public Int32 Questions { get; private set; }

		public List<String> Errors { get; } = [];

		public void ShowMessage(String message, String? caption = null)
		{
		}

		public void ShowError(String message, String? caption = null)
		{
			Errors.Add(message);
		}

		public Boolean Confirm(String message, String? caption = null)
		{
			Questions++;
			return true;
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
			return null;
		}

		public String? PickFile(String filter, String initialDirectory, String? initialFileName = null, Boolean checkFileExists = true)
		{
			return null;
		}

		public String? PickFolder(String initialDirectory, String? title = null)
		{
			return null;
		}
	}
}