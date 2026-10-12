namespace SFallConfigurator.Views
{
	using Services;

	/// <summary>
	/// The theme service of the window: the choice is remembered by the base class, and applied to the application.
	/// </summary>
	internal sealed class ThemeService : MemoryThemeService
	{
		public override void SetPreference(ThemePreference preference)
		{
			base.SetPreference(preference);
			ThemeManager.Apply(preference);
		}
	}
}