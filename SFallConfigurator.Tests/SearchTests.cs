namespace SFallConfigurator.Tests
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Configuration;
	using Infrastructure;
	using Localization;
	using Options;
	using ViewModels;
	using Xunit;

	/// <summary>
	/// The search of an option, and the way it takes the user there.
	/// </summary>
	public sealed class SearchTests
	{
		private static MainViewModel Open(TestFolder folder)
		{
			GameConfiguration configuration = new(folder.Path);
			configuration.Load();
			MainViewModel viewModel = new(configuration, new AppServices(new TestInteraction()), new TestHost());
			viewModel.Load();
			return viewModel;
		}

		private static TestFolder CreateGame()
		{
			TestFolder folder = new();
			folder.Write("ddraw.ini", "[Main]\r\nHiResMode=1\r\n[Misc]\r\nExtraSaveSlots=0\r\n");
			folder.Write("f2_res.ini", "[MAIN]\r\nSCR_WIDTH=800\r\nSCR_HEIGHT=600\r\n");
			folder.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\nmaster_dat=master.dat\r\n");
			return folder;
		}

		[Fact]
		public void AnOptionIsFoundByItsLabel()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);

			viewModel.Search.Query = "save slots";

			Assert.Contains(viewModel.Search.Results, result => result.Option.Key == "ExtraSaveSlots");
		}

		[Fact]
		public void TheSearchIgnoresCaseAndAccents()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);

			viewModel.Search.Query = "SAVÉ SLOTS";

			Assert.Contains(viewModel.Search.Results, result => result.Option.Key == "ExtraSaveSlots");
		}

		[Fact]
		public void EveryWordMustBeThere()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);

			viewModel.Search.Query = "slots zzzzzz";

			Assert.Empty(viewModel.Search.Results);
			Assert.True(viewModel.Search.HasNoResult);
		}

		[Fact]
		public void AnOptionIsFoundByItsKeyAndByItsExplanation()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);

			viewModel.Search.Query = "master_dat";
			Assert.Contains(viewModel.Search.Results, result => result.Option.Key == "master_dat" && result.Section == MainSection.Settings);

			viewModel.Search.Query = "karma";
			Assert.NotEmpty(viewModel.Search.Results);
		}

		[Fact]
		public void TheLabelCountsMoreThanTheExplanation()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);

			viewModel.Search.Query = "karma";

			Assert.Contains("karma", viewModel.Search.Results[0].Title, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void OpeningAResultGoesToItsPageAndMarksTheOption()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);
			List<NavigationTarget> targets = [];
			viewModel.NavigationRequested += (_, target) => targets.Add(target);
			viewModel.Search.Query = "save slots";

			viewModel.Search.Open();

			NavigationTarget go = Assert.Single(targets);
			Assert.Equal(MainSection.Sfall, go.Section);
			Assert.Same(go.Page, viewModel.Sfall.SelectedPage);
			Assert.True(go.Option.IsHighlighted);
			Assert.Contains(go.Option, go.Page!.Options);
		}

		[Fact]
		public void TheSearchBarForgetsTheQueryWhenItIsClosed()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);
			viewModel.Search.IsVisible = true;
			viewModel.Search.Query = "slots";

			viewModel.Search.IsVisible = false;

			Assert.Equal(String.Empty, viewModel.Search.Query);
			Assert.Empty(viewModel.Search.Results);
		}

		[Fact]
		public void EveryOptionThePagesShowCanBeFound()
		{
			using TestFolder folder = CreateGame();
			MainViewModel viewModel = Open(folder);

			foreach (ConfigOption option in viewModel.AllOptions.Where(option => option.Id is not null && !option.Label.Contains("..", StringComparison.Ordinal)))
			{
				viewModel.Search.Query = option.Key;
				Assert.True(viewModel.Search.Results.Any(result => result.Option == option) || viewModel.Search.Results.Count == 40, option.Key + " is not found");
			}
		}
	}
}