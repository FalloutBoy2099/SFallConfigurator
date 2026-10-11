namespace SFallConfigurator.Views
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.InteropServices;
	using System.Windows;
	using System.Windows.Interop;
	using System.Windows.Shell;
	using System.Windows.Threading;

	/// <summary>
	/// Gives the title bar of a window the dark or light look of the application theme.
	/// </summary>
	internal static partial class TitleBarTheme
	{
		// DWMWA_USE_IMMERSIVE_DARK_MODE: the documented value is 20; 19 is the one of the first Windows 10 builds that had it.
		private const Int32 ImmersiveDarkMode = 20;
		private const Int32 ImmersiveDarkModeBeforeBuild18985 = 19;

		// SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED: only redraws the frame.
		private const UInt32 RedrawFrame = 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020;

		private static readonly TimeSpan NativeDialogCheckInterval = TimeSpan.FromMilliseconds(30);

		/// <summary>
		/// Applies the theme to every window that is created, so that no dialog keeps a light title bar.
		/// The windows of the program also call <see cref="Attach"/>, which acts before the first paint.
		/// </summary>
		public static void ApplyToAllWindows()
		{
			EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
		}

		/// <summary>
		/// Sets the title bar of the window as soon as it has a handle, before it is shown: a bar set later is first
		/// drawn in the colour of the system (white on a light system) and may stay so until the window is redrawn.
		/// </summary>
		public static void Attach(Window window)
		{
			window.SourceInitialized += (_, _) => Apply(window);
		}

		/// <summary>
		/// Gives the title bar of an open window the look of the current theme.
		/// </summary>
		public static void Apply(Window window)
		{
			IntPtr handle = new WindowInteropHelper(window).Handle;
			if (handle == IntPtr.Zero)
			{
				return;
			}

			Apply(handle);

			// A window that draws its own title bar has no frame to redraw.
			if (window.IsVisible && WindowChrome.GetWindowChrome(window) is null)
			{
				Redraw(handle);
			}
		}

		/// <summary>
		/// Darkens the title bar of the dialogs of Windows (the file and folder choosers) that open while the returned
		/// object is alive. The program does not create them, so the title bar is set as soon as they show up.
		/// </summary>
		public static IDisposable WatchNativeDialogs()
		{
			return new NativeDialogWatcher();
		}

		private static void OnWindowLoaded(Object sender, RoutedEventArgs e)
		{
			if (sender is Window window)
			{
				Apply(window);
			}
		}

		private static void Apply(IntPtr handle)
		{
			Int32 dark = ThemeManager.IsDark() ? 1 : 0;
			if (SetWindowAttribute(handle, ImmersiveDarkMode, ref dark, sizeof(Int32)) != 0)
			{
				SetWindowAttribute(handle, ImmersiveDarkModeBeforeBuild18985, ref dark, sizeof(Int32));
			}
		}

		private static void Redraw(IntPtr handle)
		{
			SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, RedrawFrame);
		}

		[LibraryImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
		private static partial Int32 SetWindowAttribute(IntPtr window, Int32 attribute, ref Int32 value, Int32 size);

		[LibraryImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static partial Boolean SetWindowPos(IntPtr window, IntPtr insertAfter, Int32 x, Int32 y, Int32 width, Int32 height, UInt32 flags);

		[LibraryImport("user32.dll")]
		private static partial IntPtr GetForegroundWindow();

		[LibraryImport("user32.dll")]
		private static partial UInt32 GetWindowThreadProcessId(IntPtr window, out UInt32 processId);

		/// <summary>
		/// A dialog of Windows runs its own message loop and blocks the caller, but the timer of the dispatcher still
		/// ticks: each tick looks for a new window of this process that is not a window of WPF, and themes it.
		/// </summary>
		private sealed class NativeDialogWatcher : IDisposable
		{
			private readonly DispatcherTimer _timer;
			private readonly HashSet<IntPtr> _themed = [];

			public NativeDialogWatcher()
			{
				_timer = new DispatcherTimer(DispatcherPriority.Send) { Interval = NativeDialogCheckInterval };
				_timer.Tick += (_, _) => ThemeForegroundDialog();
				_timer.Start();
			}

			public void Dispose()
			{
				_timer.Stop();
			}

			private void ThemeForegroundDialog()
			{
				IntPtr window = GetForegroundWindow();
				if (window == IntPtr.Zero || _themed.Contains(window) || HwndSource.FromHwnd(window) is not null)
				{
					return;
				}

				GetWindowThreadProcessId(window, out UInt32 processId);
				if (processId != (UInt32)Environment.ProcessId)
				{
					return;
				}

				_themed.Add(window);
				Apply(window);
				Redraw(window);
			}
		}
	}
}