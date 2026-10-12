namespace SFallConfigurator.Infrastructure
{
	using System;
	using Services;

	/// <summary>
	/// The services the view models use. Every one but the user interaction has a real implementation by default; a test
	/// replaces the ones it needs to control.
	/// </summary>
	internal sealed class AppServices
	{
		public AppServices(IUserInteraction interaction)
		{
			Interaction = interaction;
		}

		public IUserInteraction Interaction { get; }

		public IFileSystem FileSystem { get; init; } = new SystemFileSystem();

		public IProcessLauncher ProcessLauncher { get; init; } = new SystemProcessLauncher();

		public IExecutablePatcher ExecutablePatcher { get; init; } = new SystemExecutablePatcher();

		public IDisplayModes DisplayModes { get; init; } = new SystemDisplayModes();

		public IThemeService Theme { get; init; } = new MemoryThemeService();
	}
}