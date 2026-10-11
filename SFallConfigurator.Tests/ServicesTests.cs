namespace SFallConfigurator.Tests
{
	using System;
	using System.IO;
	using System.Text;
	using Services;
	using Xunit;

	public class ServicesTests
	{
		/// <summary>
		/// The algorithm of the original tool, kept as the reference the new code must agree with.
		/// </summary>
		private static UInt32 ReferenceChecksum(Byte[] bytes)
		{
			UInt32[] table = new UInt32[256];
			UInt32 crc = 0xFFFFFFFF;
			for (UInt16 i = 0; i <= 255; i++)
			{
				UInt32 r = i;
				for (Byte j = 0; j < 8; j++)
				{
					r = (r & 1) != 0 ? (r >> 1) ^ 0x1EDC6F41 : r >> 1;
				}

				table[i] = r;
			}

			for (UInt32 i = 0; i < bytes.Length; i++)
			{
				crc = table[(Byte)(crc & 0xFF) ^ bytes[i]] ^ (crc >> 8);
			}

			return crc ^ 0xFFFFFFFF;
		}

		[Fact]
		public void ChecksumMatchesTheOriginalAlgorithm()
		{
			Byte[] data = new Byte[5000];
			new Random(42).NextBytes(data);

			Assert.Equal(ReferenceChecksum(data), ExeChecksum.Compute(data));
			Assert.Equal(ReferenceChecksum([]), ExeChecksum.Compute([]));
		}

		[Fact]
		public void ChecksumListRecognizesHexWithAndWithoutPrefix()
		{
			using TestFolder folder = new();
			String exe = folder.Combine("game.exe");
			File.WriteAllBytes(exe, Encoding.ASCII.GetBytes("not really an executable"));
			String hex = ExeChecksum.ComputeHex(exe);

			Assert.False(ExeChecksum.IsListed(exe, String.Empty));
			Assert.False(ExeChecksum.IsListed(exe, "0x1, 0x2"));
			Assert.True(ExeChecksum.IsListed(exe, "0x1, 0x" + hex.ToLowerInvariant()));
			Assert.True(ExeChecksum.IsListed(exe, hex));

			Assert.Equal("0x" + hex, ExeChecksum.AppendTo(String.Empty, exe));
			Assert.Equal("0x1, 0x" + hex, ExeChecksum.AppendTo("0x1", exe));
		}

		private static String CreateFakeExecutable(TestFolder folder, Boolean valid)
		{
			Byte[] image = new Byte[1100000];
			if (valid)
			{
				Encoding.ASCII.GetBytes("GNW95MUTEX").CopyTo(image, 1040600);
				Encoding.ASCII.GetBytes("DDRAW").CopyTo(image, 0x000FE150);
			}

			String path = folder.Combine(valid ? "fallout2.exe" : "other.exe");
			File.WriteAllBytes(path, image);
			return path;
		}

		[Fact]
		public void PatcherRecognizesAFallout2Executable()
		{
			using TestFolder folder = new();

			Assert.True(ExePatcher.IsValid(CreateFakeExecutable(folder, valid: true)));
			Assert.False(ExePatcher.IsValid(CreateFakeExecutable(folder, valid: false)));
			Assert.False(ExePatcher.IsValid(folder.Combine("missing.exe")));
		}

		[Fact]
		public void HiResPatchCanBeEnabledAndDisabled()
		{
			using TestFolder folder = new();
			String exe = CreateFakeExecutable(folder, valid: true);

			Assert.False(ExePatcher.IsHiResPatchEnabled(exe));
			Assert.True(ExePatcher.SetHiResPatch(exe, enable: true));
			Assert.True(ExePatcher.IsHiResPatchEnabled(exe));
			Assert.True(ExePatcher.SetHiResPatch(exe, enable: false));
			Assert.False(ExePatcher.IsHiResPatchEnabled(exe));
		}

		[Fact]
		public void SfallDllNameCanBeSwitched()
		{
			using TestFolder folder = new();
			String exe = CreateFakeExecutable(folder, valid: true);

			Assert.Equal(DllPatchState.Original, ExePatcher.GetSfallDllState(exe));
			Assert.True(ExePatcher.SetSfallDll(exe, enable: true));
			Assert.Equal(DllPatchState.Patched, ExePatcher.GetSfallDllState(exe));
			Assert.True(ExePatcher.SetSfallDll(exe, enable: false));
			Assert.Equal(DllPatchState.Original, ExePatcher.GetSfallDllState(exe));
			Assert.Equal(DllPatchState.Unknown, ExePatcher.GetSfallDllState(folder.Combine("missing.exe")));
		}

		[Fact]
		public void PatcherRefusesAnotherExecutable()
		{
			using TestFolder folder = new();
			String exe = CreateFakeExecutable(folder, valid: false);

			Assert.False(ExePatcher.SetHiResPatch(exe, enable: true));
			Assert.False(ExePatcher.SetSfallDll(exe, enable: true));
		}

		[Fact]
		public void DisplayModesAreLargestFirstAndAtLeast640x480()
		{
			System.Collections.Generic.IReadOnlyList<ScreenResolution> modes = DisplayModes.GetAvailable();

			Assert.All(modes, mode => Assert.True(mode.Width >= 640 && mode.Height >= 480));
			for (Int32 index = 1; index < modes.Count; index++)
			{
				Assert.True(modes[index - 1].Width > modes[index].Width
					|| (modes[index - 1].Width == modes[index].Width && modes[index - 1].Height > modes[index].Height));
			}

			Assert.All(DisplayModes.GetAvailable(scaledX2: true), mode => Assert.True(mode.Width >= 1280 && mode.Height >= 960));
		}
	}
}