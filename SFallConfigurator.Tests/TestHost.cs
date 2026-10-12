namespace SFallConfigurator.Tests
{
	using System;
	using System.Collections.Generic;
	using Infrastructure;

	/// <summary>
	/// The application that owns the window, for the tests: it only records what the view model asked of it.
	/// </summary>
	internal sealed class TestHost : IAppHost
	{
		public Int32 CloseCount { get; private set; }

		public Int32 ReloadCount { get; private set; }

		public List<String> ChangedFolders { get; } = [];

		public List<String> ChangedLanguages { get; } = [];

		public void Close()
		{
			CloseCount++;
		}

		public List<String?> ReloadNotices { get; } = [];

		public void Reload(String? notice = null)
		{
			ReloadCount++;
			ReloadNotices.Add(notice);
		}

		public void ChangeGameFolder(String gamePath)
		{
			ChangedFolders.Add(gamePath);
		}

		public void ChangeLanguage(String code)
		{
			ChangedLanguages.Add(code);
		}
	}
}