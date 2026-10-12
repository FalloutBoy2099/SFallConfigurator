# SFallConfigurator — by Kain
A program to quickly configure the options of sfall, HRP and some parameters of the game.

A rewrite in .NET 10 / WPF of SF-Configurator by Mr.STALIN.

#### Additional features:
+ Quickly assign/change the key codes of the options.
+ Transfer the settings of the previous version of the config to a new config (only the settings of options that exist in the new config are transferred).
+ When the resolution is changed in the configurator, the resolution in the HRP file `f2_res.ini` is changed too (optional).
+ The CRC values are calculated automatically when the game is started with the **Run Game** button.
+ The comments of the <kbd>.dat</kbd> files can be edited and written.
+ **Search** (<kbd>Ctrl</kbd>+<kbd>F</kbd>, or the magnifier in the title bar): finds an option by its name, its key or its explanation, and takes you to its page.
+ **Undo and redo, per option**: right-click an option (or <kbd>Ctrl</kbd>+<kbd>Z</kbd> / <kbd>Ctrl</kbd>+<kbd>Y</kbd> on the option that has the focus). The menu also restores the default value, when it is known, and the value that was saved. The help of an option (the pointer on it) shows its explanation, its default value and the file and key where it is stored.
+ **Changes** (menu *Tools*): *Review the changes...* lists, file by file, what saving will change (before / after). With *Review the changes before saving*, each save first shows that list and asks to go on.
+ **Profiles** (menu *Tools*): *Save the settings as a profile...* keeps the values of the options (not the paths of the game); *Apply a profile* gives them to the options of another game folder, and each value can be undone. Profiles are small text files in `%LOCALAPPDATA%\SFallConfigurator\profiles`.
+ **Check the installation...** (menu *Tools*): shows what is installed (game executable, sfall and its version, Hi-Res patch, mods) and what is likely to be a problem (missing files, two Hi-Res patches, read-only files, a protected folder).
+ **Backups**: each time the settings are saved, the previous version of the changed files is copied to the folder `old_ini` of the game (one sub-folder per save, the last 20 are kept). *Restore a backup...* (menu *Tools*) puts one back; the current files are kept in a new backup.
+ **Several windows**: a game folder is edited by one window at a time (the program asks before opening it a second time).
+ **Files changed by another program** (an editor, a restored backup) are noticed: the settings are read again at once when nothing would be lost, and a notice with a *Read again* button is shown otherwise. The menu *Tools* turns this off.

#### Build and run (.NET 10 / WPF, Fluent theme)
+ Requires the **.NET 10 SDK** to build (Windows only). To run, `SFallConfigurator.exe` needs the **.NET 10 Desktop Runtime**; `SFallConfigurator-NET.exe` has it inside and needs nothing.
+ `dotnet build SFallConfigurator.slnx` builds, `dotnet test SFallConfigurator.slnx` runs the unit tests. The solution has three libraries and a program: `SFallConfigurator.Core` (files, services), `SFallConfigurator.ViewModels` (the options and the view models, no reference to WPF), `SFallConfigurator` (the WPF views) and `SFallConfigurator.Tests`.
+ The options are described by data: `SFallConfigurator.ViewModels\Options\Catalog\*.json` say, for every option, where it is stored, how it is called, what it accepts and what its default is; the pages add the behaviour that data cannot say. A new option is one entry of a JSON file (and its translations).
+ The rules of the style are checked by the build (`.editorconfig`, the analyzers of the SDK in *Recommended* mode; the CI treats the warnings as errors) and by the tests `SourceStyleTests` (tabs, CRLF, no `var`, the usings inside the namespace, the comments in English).
+ `SF_CONFIGURATOR_E2E=1 dotnet test SFallConfigurator.slnx --filter EndToEndTests` starts the real program and drives it through UI Automation (no mouse, no keyboard): it opens its window for a few seconds. The program keeps its settings in the folder named by the environment variable `SFALLCONFIGURATOR_SETTINGS_DIR` when it is set.
+ The XAML files are formatted with [XamlStyler](https://github.com/Xavalon/XamlStyler) (local tool, see `dotnet-tools.json` and `Settings.XamlStyler`): `dotnet tool restore`, then `dotnet xstyler -d SFallConfigurator -r` (add `-p` to only check).
+ `.\Publish.ps1` creates two single-file executables in the `publish` folder: `SFallConfigurator.exe` (about 1.6 MB, needs the .NET 10 Desktop Runtime) and `SFallConfigurator-NET.exe` (about 60 MB, the .NET runtime is included).
+ GitHub Actions (`.github/workflows/ci.yml`) checks the format of the XAML files, builds (warnings are errors), runs the tests and publishes the two Windows x64 executables: they are kept as the artifact `SFallConfigurator-win-x64` of each run. Pushing a tag such as `git tag v2026.10.12 && git push origin v2026.10.12` also creates a GitHub release with both files.
+ Put the executable in the game folder (next to `ddraw.ini`), or choose the game folder with the button at the top of the window.
+ The game folder can also be given on the command line: `SFallConfigurator.exe --game "C:\Games\Fallout2"`. The last used folder is remembered.
+ The language of the interface (English, Czech, French, German, Hungarian, Italian, Korean, Polish, Portuguese, Russian, Spanish, Swedish and Traditional Chinese: the languages of the Fallout 2 Restoration Project and of the Fallout 2 community translations) follows Windows (English by default); the language button in the title bar changes it. The theme button next to it switches between Automatic (follows Windows), Light and Dark. The window size and position, the language, the theme and the last 10 game folders are remembered in `%LOCALAPPDATA%\SFallConfigurator\settings.txt`; the last used game folder is opened at the next start.
+ The installer mode merges the settings saved in `ddraw.ini.bak` and `sfall-mods.ini.bak` into the new files, then ends: `SFallConfigurator.exe -install +` (the flag `+` deletes the backups, `-` keeps them), optionally followed by the path of `sfall-mods.ini` relative to the game folder. Settings of the former name of the program (`%LOCALAPPDATA%\SFallConfiguratorMk2`) are copied once to the new folder.
+ The **Mods** tab edits the load order of `mods\mods_order.ini` (drag the mods, or use the buttons). The load order and the configuration files are saved together: when one file cannot be written, none of them is changed. Before the first patch of the game executable (Hi-Res by Mash, `SFALL.DLL`) a copy of the original is kept as `name.exe.bak`.
+ Unexpected errors are shown in a dialog and written, with their details, to `%LOCALAPPDATA%\SFallConfigurator\error.log`.
+ Optional checks against real data: set `SF_CONFIGURATOR_TEST_GAME` to a game folder and `SF_CONFIGURATOR_TEST_DAT` to a folder of `.dat` files before running the tests.