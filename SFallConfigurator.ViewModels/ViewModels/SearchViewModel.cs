namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using System.Windows.Input;
	using Infrastructure;
	using Localization;
	using Options;

	/// <summary>
	/// The tabs of the main window that hold options.
	/// </summary>
	internal enum MainSection
	{
		Sfall,
		HiRes,
		Mods,
		Settings,
	}

	/// <summary>
	/// An option that a search found, and where it is.
	/// </summary>
	internal sealed class SearchResult
	{
		public SearchResult(MainSection section, OptionPage? page, ConfigOption option, String path, Int32 score)
		{
			Section = section;
			Page = page;
			Option = option;
			Path = path;
			Score = score;
		}

		public MainSection Section { get; }

		/// <summary>
		/// The page of the option inside its tab, or null for a tab that has no pages.
		/// </summary>
		public OptionPage? Page { get; }

		public ConfigOption Option { get; }

		/// <summary>
		/// Where to find the option: the tab and the page.
		/// </summary>
		public String Path { get; }

		public Int32 Score { get; }

		public String Title => Option.PlainLabel;
	}

	/// <summary>
	/// Where the program must go to show an option.
	/// </summary>
	internal sealed record NavigationTarget(MainSection Section, OptionPage? Page, ConfigOption Option);

	/// <summary>
	/// Looks for an option by what it is called and what its explanation says.
	/// </summary>
	internal sealed class SearchViewModel : ObservableObject
	{
		private const Int32 MaximumResults = 40;

		private readonly Func<IEnumerable<(MainSection Section, String SectionName, OptionPage? Page, IEnumerable<ConfigOption> Options)>> _locations;
		private readonly Action<NavigationTarget> _navigate;

		private String _query = String.Empty;
		private Boolean _isVisible;
		private SearchResult? _selectedResult;

		public SearchViewModel(Func<IEnumerable<(MainSection Section, String SectionName, OptionPage? Page, IEnumerable<ConfigOption> Options)>> locations, Action<NavigationTarget> navigate)
		{
			_locations = locations;
			_navigate = navigate;
			OpenCommand = new RelayCommand(Open, () => SelectedResult is not null || Results.Count > 0);
			ToggleCommand = new RelayCommand(() => IsVisible = !IsVisible);
			ShowCommand = new RelayCommand(() => IsVisible = true);
			CloseCommand = new RelayCommand(() => IsVisible = false);
		}

		public ObservableCollection<SearchResult> Results { get; } = [];

		public ICommand OpenCommand { get; }

		public ICommand ToggleCommand { get; }

		public ICommand ShowCommand { get; }

		public ICommand CloseCommand { get; }

		/// <summary>
		/// True while the search bar is shown.
		/// </summary>
		public Boolean IsVisible
		{
			get => _isVisible;
			set
			{
				if (SetProperty(ref _isVisible, value) && !value)
				{
					Query = String.Empty;
				}
			}
		}

		public String Query
		{
			get => _query;
			set
			{
				if (SetProperty(ref _query, value))
				{
					Refresh();
				}
			}
		}

		public SearchResult? SelectedResult
		{
			get => _selectedResult;
			set => SetProperty(ref _selectedResult, value);
		}

		public Boolean HasResults => Results.Count > 0;

		/// <summary>
		/// True when something was typed and nothing matches.
		/// </summary>
		public Boolean HasNoResult => Query.Trim().Length > 0 && Results.Count == 0;

		/// <summary>
		/// Finds the options again (after a change of the language, the texts are not the same).
		/// </summary>
		public void Refresh()
		{
			Results.Clear();
			String[] words = Split(Query);
			if (words.Length > 0)
			{
				foreach (SearchResult result in Find(words).OrderByDescending(result => result.Score).Take(MaximumResults))
				{
					Results.Add(result);
				}
			}

			SelectedResult = Results.FirstOrDefault();
			OnPropertyChanged(nameof(HasResults));
			OnPropertyChanged(nameof(HasNoResult));
		}

		/// <summary>
		/// Goes to the selected option, or to the first one.
		/// </summary>
		public void Open()
		{
			SearchResult? result = SelectedResult ?? Results.FirstOrDefault();
			if (result is not null)
			{
				_navigate(new NavigationTarget(result.Section, result.Page, result.Option));
			}
		}

		private IEnumerable<SearchResult> Find(String[] words)
		{
			HashSet<ConfigOption> seen = [];
			foreach ((MainSection section, String sectionName, OptionPage? page, IEnumerable<ConfigOption> options) in _locations())
			{
				String path = page is null ? Localizer.T(sectionName) : Localizer.T(sectionName) + " > " + page.Header;
				foreach (ConfigOption option in options)
				{
					if (!seen.Add(option))
					{
						continue;
					}

					Int32 score = Score(option, words);
					if (score > 0)
					{
						yield return new SearchResult(section, page, option, path, score);
					}
				}
			}
		}

		/// <summary>
		/// Zero when a word is nowhere in the option. The label counts most, then the key, then the explanation.
		/// </summary>
		private static Int32 Score(ConfigOption option, String[] words)
		{
			String label = Normalize(option.Label);
			String key = Normalize(option.Key);
			String help = Normalize(option.ToolTip ?? String.Empty);
			Int32 score = 0;
			foreach (String word in words)
			{
				if (label.StartsWith(word, StringComparison.Ordinal))
				{
					score += 12;
				}
				else if (label.Contains(word, StringComparison.Ordinal))
				{
					score += 10;
				}
				else if (key.Contains(word, StringComparison.Ordinal))
				{
					score += 6;
				}
				else if (help.Contains(word, StringComparison.Ordinal))
				{
					score += 2;
				}
				else
				{
					return 0;
				}
			}

			return score;
		}

		private static String[] Split(String text)
		{
			return Normalize(text).Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
		}

		/// <summary>
		/// Lower case without the accents of the letters, so that an accented word is found without typing them.
		/// </summary>
		private static String Normalize(String text)
		{
			StringBuilder result = new(text.Length);
			foreach (Char character in text.Normalize(NormalizationForm.FormD))
			{
				if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
				{
					result.Append(Char.ToLowerInvariant(character));
				}
			}

			return result.ToString();
		}
	}
}