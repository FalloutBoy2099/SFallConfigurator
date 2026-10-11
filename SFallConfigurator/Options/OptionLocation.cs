namespace SFallConfigurator.Options
{
	using System;
	using Configuration;

	/// <summary>
	/// A configuration file (through its source) and optionally a section of it.
	/// Without a section the key is looked up in the whole file.
	/// </summary>
	internal readonly record struct OptionLocation(IConfigSource Source, String? Section = null)
	{
		public OptionLocation In(String section)
		{
			return this with { Section = section };
		}
	}
}