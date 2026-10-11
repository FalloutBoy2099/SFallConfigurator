namespace SFallConfigurator.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using Localization;

	/// <summary>
	/// DirectInput keyboard scan codes (DIK_*) accepted by the sfall key options.
	/// </summary>
	internal static class DirectInputKeys
	{
		internal const Int32 ModifierControl = -1;
		internal const Int32 ModifierAlt = -2;
		internal const Int32 ModifierShift = -3;

		private static readonly KeyInfo[] Keys =
		[
			new KeyInfo(1, "DIK_ESCAPE", "Escape"),
			new KeyInfo(2, "DIK_1", "1"),
			new KeyInfo(3, "DIK_2", "2"),
			new KeyInfo(4, "DIK_3", "3"),
			new KeyInfo(5, "DIK_4", "4"),
			new KeyInfo(6, "DIK_5", "5"),
			new KeyInfo(7, "DIK_6", "6"),
			new KeyInfo(8, "DIK_7", "7"),
			new KeyInfo(9, "DIK_8", "8"),
			new KeyInfo(10, "DIK_9", "9"),
			new KeyInfo(11, "DIK_0", "0"),
			new KeyInfo(12, "DIK_MINUS", "-"),
			new KeyInfo(13, "DIK_EQUALS", "="),
			new KeyInfo(14, "DIK_BACKSPACE", "Backspace"),
			new KeyInfo(15, "DIK_TAB", "Tab"),
			new KeyInfo(16, "DIK_Q", "Q"),
			new KeyInfo(17, "DIK_W", "W"),
			new KeyInfo(18, "DIK_E", "E"),
			new KeyInfo(19, "DIK_R", "R"),
			new KeyInfo(20, "DIK_T", "T"),
			new KeyInfo(21, "DIK_Y", "Y"),
			new KeyInfo(22, "DIK_U", "U"),
			new KeyInfo(23, "DIK_I", "I"),
			new KeyInfo(24, "DIK_O", "O"),
			new KeyInfo(25, "DIK_P", "P"),
			new KeyInfo(26, "DIK_LBRACKET", "["),
			new KeyInfo(27, "DIK_RBRACKET", "]"),
			new KeyInfo(28, "DIK_ENTER", "Enter"),
			new KeyInfo(29, "DIK_LCONTROL", "Left Ctrl"),
			new KeyInfo(30, "DIK_A", "A"),
			new KeyInfo(31, "DIK_S", "S"),
			new KeyInfo(32, "DIK_D", "D"),
			new KeyInfo(33, "DIK_F", "F"),
			new KeyInfo(34, "DIK_G", "G"),
			new KeyInfo(35, "DIK_H", "H"),
			new KeyInfo(36, "DIK_J", "J"),
			new KeyInfo(37, "DIK_K", "K"),
			new KeyInfo(38, "DIK_L", "L"),
			new KeyInfo(39, "DIK_SEMICOLON", ";"),
			new KeyInfo(40, "DIK_APOSTROPHE", "'"),
			new KeyInfo(41, "DIK_GRAVE", "`"),
			new KeyInfo(42, "DIK_LSHIFT", "Left Shift"),
			new KeyInfo(43, "DIK_BACKSLASH", "\\"),
			new KeyInfo(44, "DIK_Z", "Z"),
			new KeyInfo(45, "DIK_X", "X"),
			new KeyInfo(46, "DIK_C", "C"),
			new KeyInfo(47, "DIK_V", "V"),
			new KeyInfo(48, "DIK_B", "B"),
			new KeyInfo(49, "DIK_N", "N"),
			new KeyInfo(50, "DIK_M", "M"),
			new KeyInfo(51, "DIK_COMMA", ","),
			new KeyInfo(52, "DIK_PERIOD", "."),
			new KeyInfo(53, "DIK_SLASH", "/"),
			new KeyInfo(54, "DIK_RSHIFT", "Right Shift"),
			new KeyInfo(55, "DIK_MULTIPLY", "*"),
			new KeyInfo(56, "DIK_LALT", "Left Alt"),
			new KeyInfo(57, "DIK_SPACE", "Space"),
			new KeyInfo(58, "DIK_CAPITAL", "CapsLock"),
			new KeyInfo(59, "DIK_F1", "F1"),
			new KeyInfo(60, "DIK_F2", "F2"),
			new KeyInfo(61, "DIK_F3", "F3"),
			new KeyInfo(62, "DIK_F4", "F4"),
			new KeyInfo(63, "DIK_F5", "F5"),
			new KeyInfo(64, "DIK_F6", "F6"),
			new KeyInfo(65, "DIK_F7", "F7"),
			new KeyInfo(66, "DIK_F8", "F8"),
			new KeyInfo(67, "DIK_F9", "F9"),
			new KeyInfo(68, "DIK_F10", "F10"),
			new KeyInfo(69, "DIK_NUMLOCK", "NumLock"),
			new KeyInfo(70, "DIK_SCROLLLOCK", "ScrollLock"),
			new KeyInfo(71, "DIK_NUMPAD7", "Num 7"),
			new KeyInfo(72, "DIK_NUMPAD8", "Num 8"),
			new KeyInfo(73, "DIK_NUMPAD9", "Num 9"),
			new KeyInfo(74, "DIK_SUBTRACT", "Subtract"),
			new KeyInfo(75, "DIK_NUMPAD4", "Num 4"),
			new KeyInfo(76, "DIK_NUMPAD5", "Num 5"),
			new KeyInfo(77, "DIK_NUMPAD6", "Num 6"),
			new KeyInfo(78, "DIK_ADD", "Add"),
			new KeyInfo(79, "DIK_NUMPAD1", "Num 1"),
			new KeyInfo(80, "DIK_NUMPAD2", "Num 2"),
			new KeyInfo(81, "DIK_NUMPAD3", "Num 3"),
			new KeyInfo(82, "DIK_NUMPAD0", "Num 0"),
			new KeyInfo(83, "DIK_DECIMAL", "Decimal"),
			new KeyInfo(87, "DIK_F11", "F11"),
			new KeyInfo(88, "DIK_F12", "F12"),
			new KeyInfo(141, "DIK_NUMPADEQUALS", "Equals"),
			new KeyInfo(145, "DIK_AT", "AT"),
			new KeyInfo(146, "DIK_COLON", "COLON"),
			new KeyInfo(147, "DIK_UNDERLINE", "_"),
			new KeyInfo(149, "DIK_STOP", "STOP"),
			new KeyInfo(150, "DIK_AX", "AX"),
			new KeyInfo(151, "DIK_UNLABELED", "UNLABELED"),
			new KeyInfo(156, "DIK_NUMPADENTER", "Num Enter"),
			new KeyInfo(157, "DIK_RCONTROL", "Right Ctrl"),
			new KeyInfo(179, "DIK_NUMPADCOMMA", "Comma"),
			new KeyInfo(181, "DIK_DIVIDE", "Divide"),
			new KeyInfo(183, "DIK_SYSRQ", "PrintScreen"),
			new KeyInfo(184, "DIK_RALT", "Right Alt"),
			new KeyInfo(199, "DIK_HOME", "Home"),
			new KeyInfo(200, "DIK_UP", "UpArrow"),
			new KeyInfo(201, "DIK_PRIOR", "PageUp"),
			new KeyInfo(203, "DIK_LEFT", "LeftArrow"),
			new KeyInfo(205, "DIK_RIGHT", "RightArrow"),
			new KeyInfo(207, "DIK_END", "End"),
			new KeyInfo(208, "DIK_DOWN", "DownArrow"),
			new KeyInfo(209, "DIK_NEXT", "PageDown"),
			new KeyInfo(210, "DIK_INSERT", "Insert"),
			new KeyInfo(211, "DIK_DELETE", "Delete"),
			new KeyInfo(219, "DIK_LWIN", "Left Win"),
			new KeyInfo(220, "DIK_RWIN", "Right Win"),
			new KeyInfo(221, "DIK_APPS", "App Menu"),
		];

		private static readonly Dictionary<Int32, KeyInfo> ByCode = BuildLookup();

		internal static IReadOnlyList<KeyInfo> All => Keys;

		/// <summary>
		/// Human readable name of a key code, as shown on the key buttons.
		/// </summary>
		internal static String GetDisplayName(Int32 code, Boolean allowModifiers = false)
		{
			if (code == 0)
			{
				return Localizer.T("N/A");
			}

			if (allowModifiers)
			{
				switch (code)
				{
					case ModifierControl:
						return Localizer.T("CTRL");
					case ModifierAlt:
						return Localizer.T("ALT");
					case ModifierShift:
						return Localizer.T("SHIFT");
				}
			}

			return ByCode.TryGetValue(code, out KeyInfo key) ? Translate(key.DisplayName) : code.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Translates the names of the keys; the letters, digits, symbols, function keys and keypad digits are the same everywhere.
		/// </summary>
		private static String Translate(String name)
		{
			Boolean isKeyCap = name.Length == 1
				|| (name.StartsWith('F') && name.Length <= 3 && Char.IsDigit(name[1]))
				|| (name.StartsWith("Num ", StringComparison.Ordinal) && name.Length == 5 && Char.IsDigit(name[4]))
				|| name is "AT" or "AX";
			return isKeyCap ? name : Localizer.T(name);
		}

		private static Dictionary<Int32, KeyInfo> BuildLookup()
		{
			Dictionary<Int32, KeyInfo> lookup = new Dictionary<Int32, KeyInfo>();
			foreach (KeyInfo key in Keys)
			{
				lookup[key.Code] = key;
			}
			return lookup;
		}

		internal readonly record struct KeyInfo(Int32 Code, String Name, String DisplayName)
		{
			/// <summary>
			/// Entry text used by the key picker, for example "17  DIK_W".
			/// </summary>
			public String ListText => Code.ToString(CultureInfo.InvariantCulture).PadRight(4) + Name;
		}
	}
}