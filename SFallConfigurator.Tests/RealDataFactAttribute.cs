namespace SFallConfigurator.Tests
{
	using System;
	using Xunit;

	/// <summary>
	/// A test that needs a real game or real archives, named by an environment variable. Without the variable
	/// the test is reported as skipped, so that it cannot look like a test that passed.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method)]
	public sealed class RealDataFactAttribute : FactAttribute
	{
		public RealDataFactAttribute(String environmentVariable)
		{
			String? value = Environment.GetEnvironmentVariable(environmentVariable);
			if (String.IsNullOrEmpty(value) || !System.IO.Directory.Exists(value))
			{
				Skip = "Set " + environmentVariable + " to a folder to run this test.";
			}
		}
	}
}