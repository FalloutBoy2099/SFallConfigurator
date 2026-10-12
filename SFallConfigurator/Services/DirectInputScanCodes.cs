namespace SFallConfigurator.Services
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.InteropServices;
	using System.Windows.Input;
	using Configuration;

	/// <summary>
	/// Finds the DirectInput key code (DIK_*) of a key that the user pressed. A DIK code is the scan code of the key, that is the place
	/// of the key on the keyboard: the key that is the "A" of a French keyboard has the code of the "Q" of an American one. The keys of
	/// the right part of the keyboard (the arrows, the keypad Enter, the right Ctrl...) have the scan code plus 128.
	/// </summary>
	internal static partial class DirectInputScanCodes
	{
		private const UInt32 MapVirtualKeyToScanCodeEx = 4;
		private const UInt32 ExtendedPrefix = 0xE0;

		private static readonly HashSet<Key> ExtendedKeys =
		[
			Key.Up, Key.Down, Key.Left, Key.Right, Key.Home, Key.End, Key.PageUp, Key.PageDown, Key.Insert, Key.Delete,
			Key.RightCtrl, Key.RightAlt, Key.Divide, Key.PrintScreen, Key.LWin, Key.RWin, Key.Apps,
		];

		/// <summary>
		/// The code of the key, or null when DirectInput has no code for it. With <paramref name="allowModifiers"/> the keys Ctrl, Alt
		/// and Shift give the codes of the modifiers (-1, -2, -3), as the options that accept them expect.
		/// </summary>
		public static Int32? FromKey(Key key, Boolean allowModifiers)
		{
			if (allowModifiers)
			{
				Int32? modifier = key switch
				{
					Key.LeftCtrl or Key.RightCtrl => DirectInputKeys.ModifierControl,
					Key.LeftAlt or Key.RightAlt => DirectInputKeys.ModifierAlt,
					Key.LeftShift or Key.RightShift => DirectInputKeys.ModifierShift,
					_ => null,
				};
				if (modifier is not null)
				{
					return modifier;
				}
			}

			UInt32 scan = MapVirtualKey((UInt32)KeyInterop.VirtualKeyFromKey(key), MapVirtualKeyToScanCodeEx);
			if (scan == 0)
			{
				return null;
			}

			// The system does not always say that a key is of the right part of the keyboard (the arrows share their scan code with the keypad).
			Boolean isExtended = (scan >> 8) == ExtendedPrefix || ExtendedKeys.Contains(key);
			Int32 code = (Int32)(scan & 0xFF) | (isExtended ? 0x80 : 0);
			return DirectInputKeys.IsKnown(code, allowModifiers: false) ? code : null;
		}

		[LibraryImport("user32.dll", EntryPoint = "MapVirtualKeyW")]
		private static partial UInt32 MapVirtualKey(UInt32 code, UInt32 mapType);
	}
}