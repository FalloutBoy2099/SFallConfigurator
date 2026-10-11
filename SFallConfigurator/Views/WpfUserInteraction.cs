namespace SFallConfigurator.Views
{
	using System;
	using System.Windows;
	using Microsoft.Win32;
	using Infrastructure;
	using Localization;

	/// <summary>
	/// Message boxes and dialogs of the WPF user interface.
	/// </summary>
	internal sealed class WpfUserInteraction : IUserInteraction
	{
		private const String DefaultCaption = "SFallConfigurator";

		private readonly Func<Window?> _owner;

		public WpfUserInteraction(Func<Window?> owner)
		{
			_owner = owner;
		}

		public void ShowMessage(String message, String? caption = null)
		{
			Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Information);
		}

		public void ShowError(String message, String? caption = null)
		{
			Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
		}

		public Boolean Confirm(String message, String? caption = null)
		{
			return Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
		}

		public Boolean? AskSaveChanges(String message, String? caption = null)
		{
			return Show(message, caption, MessageBoxButton.YesNoCancel, MessageBoxImage.Question) switch
			{
				MessageBoxResult.Yes => true,
				MessageBoxResult.No => false,
				_ => null,
			};
		}

		public Int32? PickKeyCode(Int32 currentCode, Boolean allowModifiers)
		{
			KeyPickerDialog dialog = new KeyPickerDialog(currentCode, allowModifiers) { Owner = _owner() };
			return dialog.ShowDialog() == true ? dialog.SelectedCode : null;
		}

		public String? PromptText(String prompt, String? initialText, String? caption = null)
		{
			TextPromptDialog dialog = new TextPromptDialog(Localizer.T(prompt), initialText, caption is null ? null : Localizer.T(caption)) { Owner = _owner() };
			return dialog.ShowDialog() == true ? dialog.Result : null;
		}

		public String? PickFile(String filter, String initialDirectory, String? initialFileName = null, Boolean checkFileExists = true)
		{
			OpenFileDialog dialog = new OpenFileDialog
			{
				Filter = TranslateFilter(filter),
				InitialDirectory = initialDirectory,
				FileName = initialFileName ?? String.Empty,
				CheckFileExists = checkFileExists,
			};

			return ShowDialog(dialog) ? dialog.FileName : null;
		}

		public String? PickFolder(String initialDirectory, String? title = null)
		{
			OpenFolderDialog dialog = new OpenFolderDialog
			{
				InitialDirectory = initialDirectory,
				Title = Localizer.T(title ?? "Select a folder"),
			};

			return ShowDialog(dialog) ? dialog.FolderName : null;
		}

		/// <summary>
		/// Translates the names of the file types of a Windows filter, which is a list of "name|pattern" pairs.
		/// </summary>
		private static String TranslateFilter(String filter)
		{
			String[] parts = filter.Split('|');
			for (Int32 index = 0; index < parts.Length; index += 2)
			{
				parts[index] = Localizer.T(parts[index]);
			}

			return String.Join('|', parts);
		}

		private MessageBoxResult Show(String message, String? caption, MessageBoxButton buttons, MessageBoxImage image)
		{
			return MessageDialog.Show(_owner(), Localizer.TranslateIfKnown(message), Localizer.TranslateIfKnown(caption ?? DefaultCaption), buttons, image);
		}

		private Boolean ShowDialog(CommonDialog dialog)
		{
			Window? owner = _owner();

			// The dialogs of Windows get the title bar of the theme as soon as they open.
			using IDisposable watcher = TitleBarTheme.WatchNativeDialogs();
			return (owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner)) == true;
		}
	}
}