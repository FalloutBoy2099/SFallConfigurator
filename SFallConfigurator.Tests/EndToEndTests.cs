namespace SFallConfigurator.Tests
{
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Windows.Automation;
	using Xunit;

	/// <summary>
	/// A test that starts the real program. It runs only when SF_CONFIGURATOR_E2E is set to 1, because it opens the window of the
	/// program on the desktop for a few seconds.
	/// </summary>
	public sealed class EndToEndFactAttribute : FactAttribute
	{
		public EndToEndFactAttribute()
		{
			if (Environment.GetEnvironmentVariable("SF_CONFIGURATOR_E2E") != "1")
			{
				Skip = "Set SF_CONFIGURATOR_E2E=1 to run the tests that start the program.";
			}
		}
	}

	/// <summary>
	/// The program as a user meets it: started in a game folder, driven through the accessibility interface of Windows (UI Automation).
	/// Nothing here moves the mouse or types on the keyboard: the controls of the window of the program are asked to act through their
	/// patterns, so the tests never touch another window of the desktop.
	/// </summary>
	public sealed class EndToEndTests
	{
		private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

		/// <summary>
		/// A running instance of the program, in a game folder and with the settings of its own.
		/// </summary>
		private sealed class RunningProgram : IDisposable
		{
			private readonly String _settingsRoot;
			private readonly Process _process;

			public RunningProgram(String gameFolder)
			{
				// The program keeps its settings in a folder of the test: the settings of the user are not touched.
				_settingsRoot = Path.Combine(Path.GetTempPath(), "SFallConfiguratorTests", "e2e-" + Guid.NewGuid().ToString("N"));
				Directory.CreateDirectory(_settingsRoot);
				File.WriteAllText(Path.Combine(_settingsRoot, "settings.txt"), "language=en" + Environment.NewLine);

				ProcessStartInfo start = new(Path.Combine(AppContext.BaseDirectory, "SFallConfigurator.exe"))
				{
					UseShellExecute = false,
					WorkingDirectory = gameFolder,
				};
				start.ArgumentList.Add("--game");
				start.ArgumentList.Add(gameFolder);
				start.Environment["SFALLCONFIGURATOR_SETTINGS_DIR"] = _settingsRoot;
				_process = Process.Start(start)!;
				Window = WaitFor(() => FindWindow(), "the window of the program");
			}

			public AutomationElement Window { get; }

			public Boolean HasExited => _process.HasExited;

			public Boolean WaitForExit(TimeSpan time)
			{
				return _process.WaitForExit(time);
			}

			public AutomationElement Find(String name, ControlType type)
			{
				try
				{
					return WaitFor(() => Window.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.NameProperty, name), new PropertyCondition(AutomationElement.ControlTypeProperty, type))), name);
				}
				catch (TimeoutException exception)
				{
					// What the window shows, to see what is wrong.
					String shown = String.Join(" | ", Window.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>().Take(80).Select(element => element.Current.ControlType.ProgrammaticName + ":" + element.Current.Name));
					throw new TimeoutException(exception.Message + " - window '" + Window.Current.Name + "' shows: " + shown, exception);
				}
			}

			public void Dispose()
			{
				if (!_process.HasExited)
				{
					_process.Kill(entireProcessTree: true);
					_process.WaitForExit(5000);
				}

				_process.Dispose();
				try
				{
					Directory.Delete(_settingsRoot, recursive: true);
				}
				catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
				{
					// A leftover temporary folder is harmless.
				}
			}

			private AutomationElement? FindWindow()
			{
				_process.Refresh();
				return _process.HasExited
					? throw new InvalidOperationException("The program ended at its start.")
					: AutomationElement.RootElement.FindFirst(TreeScope.Children, new AndCondition(new PropertyCondition(AutomationElement.ProcessIdProperty, _process.Id), new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window)));
			}
		}

		private static AutomationElement WaitFor(Func<AutomationElement?> find, String what)
		{
			DateTime end = DateTime.UtcNow + Patience;
			while (DateTime.UtcNow < end)
			{
				AutomationElement? found = find();
				if (found is not null)
				{
					return found;
				}

				Thread.Sleep(100);
			}

			throw new TimeoutException("Not found in time: " + what);
		}

		private static TestFolder CreateGame()
		{
			TestFolder folder = new();
			folder.Write("ddraw.ini", "[Main]\r\nHiResMode=0\r\n[Misc]\r\nExtraSaveSlots=0\r\nDisplayKarmaChanges=0\r\n");
			folder.Write("fallout2.cfg", "[system]\r\nlanguage=english\r\n");
			return folder;
		}

		private static void Invoke(AutomationElement element)
		{
			((InvokePattern)element.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
		}

		[EndToEndFact]
		public void AnOptionThatIsChangedIsSavedByDone()
		{
			using TestFolder game = CreateGame();
			using RunningProgram program = new(game.Path);

			((TogglePattern)program.Find("Enable extra save slots", ControlType.CheckBox).GetCurrentPattern(TogglePattern.Pattern)).Toggle();
			Invoke(program.Find("Done", ControlType.Button));

			Assert.True(program.WaitForExit(TimeSpan.FromSeconds(20)), "The program did not close.");
			String ddraw = File.ReadAllText(game.Combine("ddraw.ini"));
			Assert.Contains("ExtraSaveSlots=1", ddraw);
			Assert.Contains("DisplayKarmaChanges=0", ddraw);
			Assert.True(Directory.Exists(game.Combine("old_ini")), "The previous version of the file was not kept.");
		}

		[EndToEndFact]
		public void NothingIsWrittenWhenNothingIsChanged()
		{
			using TestFolder game = CreateGame();
			String before = File.ReadAllText(game.Combine("ddraw.ini"));
			using RunningProgram program = new(game.Path);

			Invoke(program.Find("Done", ControlType.Button));

			Assert.True(program.WaitForExit(TimeSpan.FromSeconds(20)), "The program did not close.");
			Assert.Equal(before, File.ReadAllText(game.Combine("ddraw.ini")));
			Assert.False(Directory.Exists(game.Combine("old_ini")));
		}

		[EndToEndFact]
		public void TheSearchFindsAnOption()
		{
			using TestFolder game = CreateGame();
			using RunningProgram program = new(game.Path);

			Invoke(program.Find("Search the options", ControlType.Button));
			AutomationElement box = program.Find("Search the options", ControlType.Edit);
			((ValuePattern)box.GetCurrentPattern(ValuePattern.Pattern)).SetValue("karma");

			AutomationElement results = program.Find("Search results", ControlType.List);
			DateTime end = DateTime.UtcNow + Patience;
			Int32 count = 0;
			while (count == 0 && DateTime.UtcNow < end)
			{
				count = results.FindAll(TreeScope.Children, Condition.TrueCondition).Count;
				Thread.Sleep(100);
			}

			Assert.True(count > 0, "The search found nothing.");
		}
	}
}