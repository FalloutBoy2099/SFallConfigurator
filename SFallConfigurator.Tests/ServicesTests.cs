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

		[Fact]
		public void PatcherRecognizesAFallout2Executable()
		{
			using TestFolder folder = new();

			Assert.True(ExePatcher.IsValid(TestExecutable.Create(folder)));
			Assert.False(ExePatcher.IsValid(TestExecutable.Create(folder, valid: false)));
			Assert.False(ExePatcher.IsValid(folder.Combine("missing.exe")));
		}

		[Fact]
		public void HiResPatchCanBeEnabledAndDisabled()
		{
			using TestFolder folder = new();
			String exe = TestExecutable.Create(folder);

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
			String exe = TestExecutable.Create(folder);

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
			String exe = TestExecutable.Create(folder, valid: false);

			Assert.False(ExePatcher.SetHiResPatch(exe, enable: true));
			Assert.False(ExePatcher.SetSfallDll(exe, enable: true));
		}

		private static readonly ScreenResolution[] SomeModes =
		[
			new ScreenResolution(800, 600),
			new ScreenResolution(1920, 1080),
			new ScreenResolution(320, 200),
			new ScreenResolution(1280, 960),
			new ScreenResolution(1920, 1080),
			new ScreenResolution(1280, 1024),
			new ScreenResolution(1024, 768),
			new ScreenResolution(1600, 900),
		];

		[Fact]
		public void DisplayModesAreLargestFirstEachOnceAndAtLeast640x480()
		{
			System.Collections.Generic.IReadOnlyList<ScreenResolution> modes = DisplayModes.Filter(SomeModes, scaledX2: false);

			Assert.Equal(
				[
					new ScreenResolution(1920, 1080),
					new ScreenResolution(1600, 900),
					new ScreenResolution(1280, 1024),
					new ScreenResolution(1280, 960),
					new ScreenResolution(1024, 768),
					new ScreenResolution(800, 600),
				],
				modes);
		}

		[Fact]
		public void DisplayModesForDoubleSizeAreAtLeast1280x960()
		{
			System.Collections.Generic.IReadOnlyList<ScreenResolution> modes = DisplayModes.Filter(SomeModes, scaledX2: true);

			Assert.Equal(
				[
					new ScreenResolution(1920, 1080),
					new ScreenResolution(1280, 1024),
					new ScreenResolution(1280, 960),
				],
				modes);
		}

		[Fact]
		public void TheDriverOfThisMachineGivesModesThatFollowTheSameRules()
		{
			// Without a display (a service, a remote session) there is nothing to check: the rules are tested above.
			Assert.All(DisplayModes.GetAvailable(), mode => Assert.True(mode.Width >= 640 && mode.Height >= 480));
			Assert.All(DisplayModes.GetAvailable(scaledX2: true), mode => Assert.True(mode.Width >= 1280 && mode.Height >= 960));
		}

	}
}