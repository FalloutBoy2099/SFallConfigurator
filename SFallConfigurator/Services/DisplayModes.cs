namespace SFallConfigurator.Services
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;

	/// <summary>
	/// A screen resolution in pixels.
	/// </summary>
	internal readonly record struct ScreenResolution(Int32 Width, Int32 Height)
	{
		public override String ToString()
		{
			return Width + " x " + Height;
		}
	}

	/// <summary>
	/// Lists the 32-bit display modes offered by the graphics driver.
	/// </summary>
	internal static partial class DisplayModes
	{
		private const Int32 MinimumWidth = 640;
		private const Int32 MinimumHeight = 480;

		/// <summary>
		/// Smallest size the "Scale X2" modes accept: half of it must still be a 640x480 game.
		/// </summary>
		private const Int32 ScaledMinimumWidth = 1280;
		private const Int32 ScaledMinimumHeight = 960;

		/// <summary>
		/// All modes of at least 640x480, largest first.
		/// </summary>
		public static IReadOnlyList<ScreenResolution> GetAvailable()
		{
			DevMode mode = new()
			{ DmSize = (UInt16)Unsafe.SizeOf<DevMode>() };
			HashSet<ScreenResolution> modes = [];

			for (Int32 index = 0; EnumDisplaySettings(null, index, ref mode); index++)
			{
				if (mode.DmBitsPerPel == 32 && mode.DmPelsWidth >= MinimumWidth && mode.DmPelsHeight >= MinimumHeight)
				{
					modes.Add(new ScreenResolution((Int32)mode.DmPelsWidth, (Int32)mode.DmPelsHeight));
				}
			}

			return modes.OrderByDescending(item => item.Width).ThenByDescending(item => item.Height).ToList();
		}

		/// <summary>
		/// Modes that can be shown at double size.
		/// </summary>
		public static IReadOnlyList<ScreenResolution> GetAvailable(Boolean scaledX2)
		{
			IReadOnlyList<ScreenResolution> modes = GetAvailable();
			return scaledX2
				? modes.Where(item => item.Height >= ScaledMinimumHeight && item.Width >= ScaledMinimumWidth).ToList()
				: modes;
		}

		[LibraryImport("user32.dll", EntryPoint = "EnumDisplaySettingsW", StringMarshalling = StringMarshalling.Utf16)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static partial Boolean EnumDisplaySettings(String? deviceName, Int32 modeNumber, ref DevMode mode);

		[InlineArray(32)]
		private struct WideCharBuffer
		{
			private UInt16 _element0;
		}

		/// <summary>
		/// The display variant of the Win32 DEVMODEW structure (220 bytes).
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		private struct DevMode
		{
			public WideCharBuffer DmDeviceName;
			public UInt16 DmSpecVersion;
			public UInt16 DmDriverVersion;
			public UInt16 DmSize;
			public UInt16 DmDriverExtra;
			public UInt32 DmFields;
			public Int32 DmPositionX;
			public Int32 DmPositionY;
			public UInt32 DmDisplayOrientation;
			public UInt32 DmDisplayFixedOutput;
			public Int16 DmColor;
			public Int16 DmDuplex;
			public Int16 DmYResolution;
			public Int16 DmTtOption;
			public Int16 DmCollate;
			public WideCharBuffer DmFormName;
			public UInt16 DmLogPixels;
			public UInt32 DmBitsPerPel;
			public UInt32 DmPelsWidth;
			public UInt32 DmPelsHeight;
			public UInt32 DmDisplayFlags;
			public UInt32 DmDisplayFrequency;
			public UInt32 DmIcmMethod;
			public UInt32 DmIcmIntent;
			public UInt32 DmMediaType;
			public UInt32 DmDitherType;
			public UInt32 DmReserved1;
			public UInt32 DmReserved2;
			public UInt32 DmPanningWidth;
			public UInt32 DmPanningHeight;
		}
	}
}