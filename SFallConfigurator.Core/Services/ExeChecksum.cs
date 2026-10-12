namespace SFallConfigurator.Services
{
	using System;
	using System.Globalization;
	using System.IO;

	/// <summary>
	/// The checksum sfall compares against its ExtraCRC list. The polynomial is applied to the
	/// reflected algorithm exactly as the original tool did, so the values stay compatible.
	/// </summary>
	internal static class ExeChecksum
	{
		private const UInt32 Polynomial = 0x1EDC6F41;

		private static readonly UInt32[] Table = BuildTable();

		public static UInt32 Compute(String path)
		{
			return Compute(File.ReadAllBytes(path));
		}

		public static UInt32 Compute(Byte[] data)
		{
			UInt32 crc = 0xFFFFFFFF;
			foreach (Byte value in data)
			{
				crc = Table[(Byte)(crc & 0xFF) ^ value] ^ (crc >> 8);
			}

			return crc ^ 0xFFFFFFFF;
		}

		/// <summary>
		/// Upper case hexadecimal text without leading zeros, for example "1A2B3C4D".
		/// </summary>
		public static String ComputeHex(String path)
		{
			return Compute(path).ToString("X", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Checks whether the checksum of <paramref name="exePath"/> is already part of the ExtraCRC list.
		/// </summary>
		public static Boolean IsListed(String exePath, String extraCrc)
		{
			return IsListed(Compute(exePath), extraCrc);
		}

		/// <summary>
		/// The entries are compared as numbers: 0x01234567, 1234567 and 0X1234567 are the same checksum.
		/// </summary>
		public static Boolean IsListed(UInt32 checksum, String extraCrc)
		{
			foreach (String entry in extraCrc.Split(','))
			{
				String value = entry.Trim();
				if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
				{
					value = value.Substring(2);
				}

				if (UInt32.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out UInt32 listed) && listed == checksum)
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Returns the ExtraCRC list with the checksum of <paramref name="exePath"/> appended.
		/// </summary>
		public static String AppendTo(String extraCrc, String exePath)
		{
			return AppendTo(extraCrc, Compute(exePath));
		}

		public static String AppendTo(String extraCrc, UInt32 checksum)
		{
			String value = "0x" + checksum.ToString("X", CultureInfo.InvariantCulture);
			return extraCrc.Length > 0 ? extraCrc + ", " + value : value;
		}

		private static UInt32[] BuildTable()
		{
			UInt32[] table = new UInt32[256];
			for (UInt32 index = 0; index < table.Length; index++)
			{
				UInt32 remainder = index;
				for (Int32 bit = 0; bit < 8; bit++)
				{
					remainder = (remainder & 1) != 0 ? (remainder >> 1) ^ Polynomial : remainder >> 1;
				}

				table[index] = remainder;
			}

			return table;
		}
	}
}