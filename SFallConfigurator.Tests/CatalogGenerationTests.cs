namespace SFallConfigurator.Tests
{
	using System;
	using System.IO;
	using System.Text.Json;
	using Configuration;
	using Infrastructure;
	using Options;
	using ViewModels;
	using Xunit;

	/// <summary>
	/// Writes the catalog files from the pages. A tool, not a check: it runs only when SF_DUMP_CATALOG names a folder.
	/// </summary>
	public sealed class CatalogGenerationTests
	{
		[RealDataFact("SF_DUMP_CATALOG")]
		public void WriteTheCatalogOfThePages()
		{
			using TestFolder game = new();
			game.Write("ddraw.ini", "[Main]\r\n");
			game.Write("f2_res.ini", "[MAIN]\r\n");
			game.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\n");
			GameConfiguration configuration = new(game.Path);
			configuration.Load();
			MainViewModel viewModel = new(configuration, new AppServices(new TestInteraction()), new TestHost());
			viewModel.Load();

			String folder = Environment.GetEnvironmentVariable("SF_DUMP_CATALOG")!;
			foreach (CatalogPage page in CatalogDumper.Dump(viewModel))
			{
				File.WriteAllText(Path.Combine(folder, page.Id + ".json"), JsonSerializer.Serialize(page, OptionCatalog.JsonOptions));
			}
		}
	}
}