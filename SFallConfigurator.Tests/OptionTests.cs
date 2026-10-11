namespace SFallConfigurator.Tests
{
	using System;
	using Configuration;
	using Infrastructure;
	using Options;
	using Xunit;

	public class OptionTests
	{
		private static (IniDocument Document, OptionLocation Location) Create(params String[] lines)
		{
			IniDocument document = new IniDocument("test.ini", lines, System.Text.Encoding.UTF8);
			return (document, new OptionLocation(new DocumentSource(() => document, IniWriteMode.UpdateExisting)));
		}

		[Fact]
		public void BoolOptionReadsAndWritesZeroOrOne()
		{
			(IniDocument ini, OptionLocation location) = Create("[Misc]", "Flag=1");
			BoolOption option = new BoolOption(location, "Flag", "Flag");

			option.Load();
			Assert.True(option.IsAvailable);
			Assert.True(option.Value);
			Assert.False(option.IsModified);

			option.Value = false;
			Assert.Equal("0", ini.GetValue(null, "Flag"));
			Assert.True(option.IsModified);

			option.Value = true;
			Assert.False(option.IsModified);
		}

		[Fact]
		public void MissingKeyMakesTheOptionUnavailableAndNothingIsWritten()
		{
			(IniDocument ini, OptionLocation location) = Create("[Misc]", "Other=1");
			BoolOption option = new BoolOption(location, "Flag", "Flag");

			option.Load();

			Assert.False(option.IsAvailable);
			Assert.False(option.IsEnabled);
			Assert.Null(option.Value);
			Assert.False(ini.IsModified);
		}

		[Fact]
		public void LoadingNeverWritesToTheFile()
		{
			(IniDocument ini, OptionLocation location) = Create("Flag=2");
			BoolOption option = new BoolOption(location, "Flag", "Flag") { TrueValue = "1" };

			option.Load();

			Assert.True(option.Value);
			Assert.Equal("2", ini.GetValue(null, "Flag"));
			Assert.False(ini.IsModified);
		}

		[Fact]
		public void BoolOptionWithCustomValuesIsInterpretedAndStoredCustomly()
		{
			(IniDocument ini, OptionLocation location) = Create("Fade=100");
			BoolOption option = new BoolOption(location, "Fade", "Fade")
			{
				TrueValue = "50",
				FalseValue = "100",
				Interpret = raw => IniValue.ToInt32(raw) == 50,
			};

			option.Load();
			Assert.False(option.Value);

			option.Value = true;
			Assert.Equal("50", ini.GetValue(null, "Fade"));
		}

		[Fact]
		public void OptionIsEnabledOnlyWhileItsConditionHolds()
		{
			(_, OptionLocation location) = Create("Master=1", "Slave=1");
			BoolOption master = new BoolOption(location, "Master", "Master");
			BoolOption slave = new BoolOption(location, "Slave", "Slave");
			slave.EnableWhen(() => master.Value == true, master);
			master.Load();
			slave.Load();

			Assert.True(slave.IsEnabled);

			Boolean notified = false;
			slave.PropertyChanged += (_, e) => notified |= e.PropertyName == nameof(ConfigOption.IsEnabled);
			master.Value = false;

			Assert.True(notified);
			Assert.False(slave.IsEnabled);
		}

		[Fact]
		public void NumberOptionClampsToItsRange()
		{
			(IniDocument ini, OptionLocation location) = Create("Count=7");
			NumberOption option = new NumberOption(location, "Count", "Count", 1, 10);
			option.Load();

			Assert.Equal(7, option.Value);

			option.Value = 50;
			Assert.Equal(10, option.Value);
			Assert.Equal("10", ini.GetValue(null, "Count"));

			option.Value = -3;
			Assert.Equal(1, option.Value);
		}

		[Fact]
		public void NumberOptionReadsHexadecimalAndUsesTheDefaultWhenMissing()
		{
			(_, OptionLocation location) = Create("Code=0x10");
			NumberOption hex = new NumberOption(location, "Code", "Code", 0, 100);
			NumberOption missing = new NumberOption(location, "Nothing", "Nothing", 0, 100) { DefaultValue = "42" };

			hex.Load();
			missing.Load();

			Assert.Equal(16, hex.Value);
			Assert.True(missing.IsAvailable);
			Assert.Equal(42, missing.Value);
		}

		[Fact]
		public void ChoiceOptionMapsValuesToItems()
		{
			(IniDocument ini, OptionLocation location) = Create("Filter=3");
			ChoiceOption option = new ChoiceOption(location, "Filter", "Filter", ["Low", "Mid", "High"]) { Values = ["1", "2", "3"] };

			option.Load();
			Assert.Equal(2, option.SelectedIndex);

			option.SelectedIndex = 0;
			Assert.Equal("1", ini.GetValue(null, "Filter"));
			Assert.Equal("1", option.SelectedValue);
		}

		[Fact]
		public void ChoiceOptionFallsBackToTheFirstItemForUnknownValues()
		{
			(_, OptionLocation location) = Create("Filter=9");
			ChoiceOption option = new ChoiceOption(location, "Filter", "Filter", ["A", "B"]);

			option.Load();

			Assert.Equal(0, option.SelectedIndex);
		}

		[Fact]
		public void KeyOptionStoresZeroWhenUnchecked()
		{
			(IniDocument ini, OptionLocation location) = Create("Key=42");
			KeyOption option = new KeyOption(location, "Key", "Key", 29, new TestInteraction());
			option.Load();

			Assert.True(option.IsChecked);
			Assert.Equal(42, option.Code);
			Assert.Equal("Left Shift", option.CodeText);

			option.IsChecked = false;
			Assert.Equal("0", ini.GetValue(null, "Key"));

			// Switching it on again brings the key back.
			option.IsChecked = true;
			Assert.Equal("42", ini.GetValue(null, "Key"));
		}

		[Fact]
		public void KeyOptionKeepsTheDefaultKeyWhileDisabledInTheFile()
		{
			(_, OptionLocation location) = Create("Key=0");
			KeyOption option = new KeyOption(location, "Key", "Key", 29, new TestInteraction());

			option.Load();

			Assert.False(option.IsChecked);
			Assert.Equal(29, option.Code);
			Assert.False(option.IsKeyEnabled);
		}

		[Fact]
		public void ChoosingAKeyStoresIt()
		{
			(IniDocument ini, OptionLocation location) = Create("Key=29");
			TestInteraction interaction = new TestInteraction { KeyToPick = 17 };
			KeyOption option = new KeyOption(location, "Key", "Key", 29, interaction);
			option.Load();

			option.ChooseKeyCommand.Execute(null);

			Assert.Equal("17", ini.GetValue(null, "Key"));
			Assert.Equal("W", option.CodeText);
		}

		[Fact]
		public void ModifierKeysAreOnlyAcceptedWhenAllowed()
		{
			(IniDocument ini, OptionLocation location) = Create("Scroll=0");
			TestInteraction interaction = new TestInteraction { KeyToPick = -2 };
			KeyOption plain = new KeyOption(location, "Scroll", "Scroll", 0, interaction) { HasToggle = false };
			KeyOption modifiers = new KeyOption(location, "Scroll", "Scroll", 0, interaction) { HasToggle = false, AllowModifiers = true };
			plain.Load();
			modifiers.Load();

			plain.ChooseKeyCommand.Execute(null);
			Assert.Equal("0", ini.GetValue(null, "Scroll"));

			modifiers.ChooseKeyCommand.Execute(null);
			Assert.Equal("-2", ini.GetValue(null, "Scroll"));
			Assert.Equal("ALT", modifiers.CodeText);
		}

		[Fact]
		public void TextOptionTrimsAndNormalizesWhatItStores()
		{
			(IniDocument ini, OptionLocation location) = Create("Path=Data");
			TextOption option = new TextOption(location, "Path", "Path") { Normalize = text => text.ToLowerInvariant() };
			option.Load();

			option.Text = "  NewFolder ";

			Assert.Equal("newfolder", ini.GetValue(null, "Path"));
		}

		[Fact]
		public void FixedTextIsShownButNeverStored()
		{
			(IniDocument ini, OptionLocation location) = Create("File=mine.ini");
			TextOption option = new TextOption(location, "File", "File");
			option.Load();

			option.ShowFixed("Imposed.ini", readOnly: true);
			option.Text = "typed";

			Assert.Equal("mine.ini", ini.GetValue(null, "File"));
			Assert.Equal("Imposed.ini", option.Text);
		}

		[Fact]
		public void BarArtOptionMapsPicturesToPresets()
		{
			(IniDocument ini, OptionLocation location) = Create("Art=2");
			BarArtOption option = new BarArtOption(location, "Art", "Art");
			option.Load();

			Assert.Equal(2, option.PresetIndex);
			Assert.EndsWith("Leather.png", option.PreviewImage, StringComparison.Ordinal);

			option.Art = 7;
			Assert.Equal(3, option.PresetIndex);
			Assert.Equal("7", ini.GetValue(null, "Art"));

			option.PresetIndex = 1;
			Assert.Equal("1", ini.GetValue(null, "Art"));
		}

		[Fact]
		public void WindowModeOptionUsesTwoKeys()
		{
			(IniDocument ini, OptionLocation location) = Create("WINDOWED=1", "WINDOWED_FULLSCREEN=1");
			WindowModeOption option = new WindowModeOption(location, "Window Mode");
			option.Load();

			Assert.Equal(2, option.SelectedIndex);

			option.SelectedIndex = 1;
			Assert.Equal("1", ini.GetValue(null, "WINDOWED"));
			Assert.Equal("0", ini.GetValue(null, "WINDOWED_FULLSCREEN"));

			option.SelectedIndex = 0;
			Assert.Equal("0", ini.GetValue(null, "WINDOWED"));
		}
	}
}