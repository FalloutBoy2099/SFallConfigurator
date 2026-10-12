namespace SFallConfigurator.Services
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The display modes that the game can use. A service, so that the editors of the resolution can be tested with
	/// the modes that the test chooses, not those of the graphics driver of the machine.
	/// </summary>
	internal interface IDisplayModes
	{
		IReadOnlyList<ScreenResolution> GetAvailable(Boolean scaledX2);
	}

	internal sealed class SystemDisplayModes : IDisplayModes
	{
		public IReadOnlyList<ScreenResolution> GetAvailable(Boolean scaledX2)
		{
			return DisplayModes.GetAvailable(scaledX2);
		}
	}
}