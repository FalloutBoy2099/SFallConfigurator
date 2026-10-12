namespace SFallConfigurator.Services
{
	using System;
	using System.Runtime.InteropServices;
	using System.Windows;

	/// <summary>
	/// The monitors of the computer: a window that was saved on a monitor that is gone, or in a dead corner of the
	/// bounding box of several monitors, must come back on a monitor that exists.
	/// </summary>
	internal static partial class MonitorAreas
	{
		private const UInt32 MonitorDefaultToNearest = 2;

		/// <summary>
		/// The work area (without the task bar) of the monitor that is nearest to the rectangle, or null when it cannot
		/// be asked. Both are in device independent units; <paramref name="dpiScale"/> is the scale of the window.
		/// </summary>
		public static Rect? GetWorkAreaNear(Rect bounds, Double dpiScale)
		{
			if (dpiScale <= 0 || !Double.IsFinite(dpiScale))
			{
				return null;
			}

			NativeRect device = new(
				(Int32)Math.Round(bounds.Left * dpiScale),
				(Int32)Math.Round(bounds.Top * dpiScale),
				(Int32)Math.Round(bounds.Right * dpiScale),
				(Int32)Math.Round(bounds.Bottom * dpiScale));
			IntPtr monitor = MonitorFromRect(ref device, MonitorDefaultToNearest);
			MonitorInfo info = new() { Size = Marshal.SizeOf<MonitorInfo>() };
			if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
			{
				return null;
			}

			return new Rect(
				info.Work.Left / dpiScale,
				info.Work.Top / dpiScale,
				(info.Work.Right - info.Work.Left) / dpiScale,
				(info.Work.Bottom - info.Work.Top) / dpiScale);
		}

		[LibraryImport("user32.dll")]
		private static partial IntPtr MonitorFromRect(ref NativeRect rect, UInt32 flags);

		[LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static partial Boolean GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

		[StructLayout(LayoutKind.Sequential)]
		private struct NativeRect(Int32 left, Int32 top, Int32 right, Int32 bottom)
		{
			public Int32 Left = left;
			public Int32 Top = top;
			public Int32 Right = right;
			public Int32 Bottom = bottom;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct MonitorInfo
		{
			public Int32 Size;
			public NativeRect Monitor;
			public NativeRect Work;
			public UInt32 Flags;
		}
	}
}