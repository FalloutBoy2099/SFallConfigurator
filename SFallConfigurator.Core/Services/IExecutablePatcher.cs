namespace SFallConfigurator.Services
{
	using System;

	/// <summary>
	/// The patches of the game executable. A service, so that the view models can be tested without an executable.
	/// </summary>
	internal interface IExecutablePatcher
	{
		Boolean IsValid(String exePath);

		Boolean IsHiResPatchEnabled(String exePath);

		Boolean SetHiResPatch(String exePath, Boolean enable);

		Boolean SetSfallDll(String exePath, Boolean enable);

		DllPatchState GetSfallDllState(String exePath);
	}

	internal sealed class SystemExecutablePatcher : IExecutablePatcher
	{
		public Boolean IsValid(String exePath)
		{
			return ExePatcher.IsValid(exePath);
		}

		public Boolean IsHiResPatchEnabled(String exePath)
		{
			return ExePatcher.IsHiResPatchEnabled(exePath);
		}

		public Boolean SetHiResPatch(String exePath, Boolean enable)
		{
			return ExePatcher.SetHiResPatch(exePath, enable);
		}

		public Boolean SetSfallDll(String exePath, Boolean enable)
		{
			return ExePatcher.SetSfallDll(exePath, enable);
		}

		public DllPatchState GetSfallDllState(String exePath)
		{
			return ExePatcher.GetSfallDllState(exePath);
		}
	}
}