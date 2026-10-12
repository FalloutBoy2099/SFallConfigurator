namespace SFallConfigurator.Services
{
	using System;
	using System.IO;
	using System.Text;
	using Localization;

	/// <summary>
	/// State of the library name stored in the game executable.
	/// </summary>
	internal enum DllPatchState
	{
		/// <summary>
		/// The file could not be read.
		/// </summary>
		Unknown,

		/// <summary>
		/// The executable loads the system ddraw.dll.
		/// </summary>
		Original,

		/// <summary>
		/// The executable loads sfall.dll.
		/// </summary>
		Patched,
	}

	/// <summary>
	/// Binary patches of the original Fallout 2 executable (offsets are file offsets of FALLOUT2.EXE 1.02).
	/// </summary>
	internal static class ExePatcher
	{
		// The "GNW95MUTEX" string at 0x50FAD8 identifies a genuine Fallout 2 executable.
		private const Int64 SignatureOffset = 1040600;
		private const String Signature = "GNW95MUTEX";

		// 0x4E4480: the call that loads f2_res.dll; 0x4FE1C0: the code and the library name it uses.
		private const Int64 HiResCallOffset = 870528;
		private const Int64 HiResCodeOffset = 976320;

		// 0x50FB50: the first half of the "DDRAW.DLL" name.
		private const Int64 DllNameOffset = 0x000FE150;

		private static readonly Byte[] HiResEnableData =
		[
			0xE8, 0x4B, 0x9D, 0x01, 0x00, 0x90, 0x90, 0x90, 0x90, 0x90,
			0x66, 0x32, 0x5F, 0x72, 0x65, 0x73, 0x2E, 0x64, 0x6C, 0x6C,
			0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x68, 0xC0, 0xE1, 0x4F, 0x00, 0x2E, 0xFF, 0x15, 0x0C, 0x02, 0x6C, 0x00, 0xC7, 0x05, 0x88, 0x27, 0x6B, 0x00, 0x00, 0xE7, 0x4D, 0x00, 0xC3,
		];

		private static readonly Byte[] HiResDisableData = [0xC7, 0x05, 0x88, 0x27, 0x6B, 0x00, 0x00, 0xE7, 0x4D, 0x00];

		private static readonly Byte[] SfallName = Encoding.ASCII.GetBytes("SFALL");
		private static readonly Byte[] DdrawName = Encoding.ASCII.GetBytes("DDRAW");

		/// <summary>
		/// Checks that the file is a Fallout 2 executable that can be patched.
		/// </summary>
		public static Boolean IsValid(String exePath)
		{
			try
			{
				using FileStream stream = new(exePath, FileMode.Open, FileAccess.Read, FileShare.Read);
				return HasSignature(stream);
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				return false;
			}
		}

		public static Boolean IsHiResPatchEnabled(String exePath)
		{
			try
			{
				using FileStream stream = new(exePath, FileMode.Open, FileAccess.Read, FileShare.Read);
				if (stream.Length < HiResCodeOffset)
				{
					return false;
				}

				stream.Seek(HiResCodeOffset, SeekOrigin.Begin);
				Byte[] libraryName = new Byte[10];
				stream.ReadExactly(libraryName, 0, libraryName.Length);

				for (Int32 index = 0; index < libraryName.Length; index++)
				{
					if (libraryName[index] != HiResEnableData[10 + index])
					{
						return false;
					}
				}

				return true;
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				return false;
			}
		}

		/// <summary>
		/// Enables or disables the built-in loader of Mash's Hi-Res Patch (f2_res.dll).
		/// </summary>
		public static Boolean SetHiResPatch(String exePath, Boolean enable)
		{
			return Patch(exePath, stream =>
			{
				stream.Seek(HiResCallOffset, SeekOrigin.Begin);
				if (enable)
				{
					stream.Write(HiResEnableData, 0, 10);
					stream.Seek(HiResCodeOffset, SeekOrigin.Begin);
					stream.Write(HiResEnableData, 10, 39);
				}
				else
				{
					stream.Write(HiResDisableData, 0, HiResDisableData.Length);
					stream.Seek(HiResCodeOffset, SeekOrigin.Begin);
					stream.Write(new Byte[10], 0, 10);
				}
			});
		}

		/// <summary>
		/// Makes the executable load sfall.dll instead of the system ddraw.dll, or the reverse.
		/// </summary>
		public static Boolean SetSfallDll(String exePath, Boolean enable)
		{
			return Patch(exePath, stream =>
			{
				stream.Seek(DllNameOffset, SeekOrigin.Begin);
				stream.Write(enable ? SfallName : DdrawName, 0, 5);
			});
		}

		/// <summary>
		/// Applies a patch to a Fallout 2 executable. A copy of the original file is kept once (name.exe.bak),
		/// and when a write fails the executable gets its bytes back, so that it is never left half patched.
		/// </summary>
		private static Boolean Patch(String exePath, Action<FileStream> apply)
		{
			try
			{
				using FileStream stream = new(exePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
				if (!HasSignature(stream))
				{
					return false;
				}

				stream.Seek(0, SeekOrigin.Begin);
				Byte[] original = new Byte[stream.Length];
				stream.ReadExactly(original, 0, original.Length);
				KeepOriginal(exePath, original);

				try
				{
					apply(stream);
					stream.Flush();
					return true;
				}
				catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
				{
					Restore(stream, original);
					return false;
				}
			}
			catch (Exception exception) when (exception is (IOException or UnauthorizedAccessException) and not ExecutableDamagedException)
			{
				return false;
			}
		}

		/// <summary>
		/// The error of a patch that failed and whose bytes could not be written back: the executable may be damaged.
		/// </summary>
		private sealed class ExecutableDamagedException : IOException
		{
			public ExecutableDamagedException()
				: base(Localizer.T("The executable may be damaged: the patch failed and the original bytes could not be written back."))
			{
			}
		}

		private static void Restore(FileStream stream, Byte[] original)
		{
			try
			{
				stream.Seek(0, SeekOrigin.Begin);
				stream.Write(original, 0, original.Length);
				stream.Flush();
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				throw new ExecutableDamagedException();
			}
		}

		private static void KeepOriginal(String exePath, Byte[] original)
		{
			String backupPath = exePath + ".bak";
			try
			{
				if (!File.Exists(backupPath))
				{
					File.WriteAllBytes(backupPath, original);
				}
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				// The copy is a courtesy: a folder that cannot take it must not stop the patch.
			}
		}

		public static DllPatchState GetSfallDllState(String exePath)
		{
			try
			{
				using FileStream stream = new(exePath, FileMode.Open, FileAccess.Read, FileShare.Read);
				if (stream.Length < DllNameOffset + SfallName.Length)
				{
					return DllPatchState.Unknown;
				}

				stream.Seek(DllNameOffset, SeekOrigin.Begin);
				Byte[] name = new Byte[SfallName.Length];
				stream.ReadExactly(name, 0, name.Length);

				return name.AsSpan().SequenceEqual(SfallName) ? DllPatchState.Patched : DllPatchState.Original;
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				return DllPatchState.Unknown;
			}
		}

		private static Boolean HasSignature(Stream stream)
		{
			if (stream.Length < SignatureOffset + Signature.Length)
			{
				return false;
			}

			stream.Seek(SignatureOffset, SeekOrigin.Begin);
			Byte[] found = new Byte[Signature.Length];
			stream.ReadExactly(found, 0, found.Length);

			return Encoding.ASCII.GetString(found) == Signature;
		}
	}
}