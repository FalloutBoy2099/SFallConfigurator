# SFallConfigurator — by Kain
A program to quickly configure the options of sfall, HRP and some parameters of the game.

A rewrite in .NET 10 / WPF of SF-Configurator by Mr.STALIN.

#### Additional features:
+ Quickly assign/change the key codes of the options.
+ Transfer the settings of the previous version of the config to a new config (only the settings of options that exist in the new config are transferred).
+ When the resolution is changed in the configurator, the resolution in the HRP file `f2_res.ini` is changed too (optional).
+ The CRC values are calculated automatically when the game is started with the **Run Game** button.
+ The comments of the <kbd>.dat</kbd> files can be edited and written.

**Example of setting Scaling x2 for sfall:**<br>
![Set scale X2](demo/Set_x2.gif)

#### Build and run (.NET 10 / WPF, Fluent theme)
+ Requires the **.NET 10 SDK** to build (Windows only). To run, `SFallConfigurator.exe` needs the **.NET 10 Desktop Runtime**; `SFallConfigurator-NET.exe` has it inside and needs nothing.
+ `dotnet build SFallConfigurator.slnx` builds, `dotnet test SFallConfigurator.slnx` runs the unit tests.
+ `.\Publish.ps1` creates two single-file executables in the `publish` folder: `SFallConfigurator.exe` (about 1 MB, needs the .NET 10 Desktop Runtime) and `SFallConfigurator-NET.exe` (about 60 MB, the .NET runtime is included).
+ Put the executable in the game folder (next to `ddraw.ini`), or choose the game folder with the button at the top of the window.
+ The game folder can also be given on the command line: `SFallConfigurator.exe --game "C:\Games\Fallout2"`. The last used folder is remembered.
+ The language of the interface (English, Czech, French, German, Hungarian, Italian, Korean, Polish, Portuguese, Russian, Spanish, Swedish and Traditional Chinese: the languages of the Fallout 2 Restoration Project and of the Fallout 2 community translations) follows Windows (English by default); the language button in the title bar changes it. The theme button next to it switches between Automatic (follows Windows), Light and Dark. The window size and position, the language, the theme and the last 10 game folders are remembered in `%LOCALAPPDATA%\SFallConfigurator\settings.txt`; the last used game folder is opened at the next start.
+ The installer mode `-install` (merge of `ddraw.ini.bak` / `sfall-mods.ini.bak`) is supported.
+ Optional checks against real data: set `SF_CONFIGURATOR_TEST_GAME` to a game folder and `SF_CONFIGURATOR_TEST_DAT` to a folder of `.dat` files before running the tests.