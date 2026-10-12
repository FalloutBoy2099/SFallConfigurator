namespace SFallConfigurator.Infrastructure
{
	using System;

	/// <summary>
	/// How serious a line of a report is.
	/// </summary>
	internal enum ReportSeverity
	{
		Ok,
		Info,
		Warning,
		Error,
	}

	/// <summary>
	/// One line of a report that the user reads: what was found, and how much it matters.
	/// </summary>
	internal sealed record ReportLine(ReportSeverity Severity, String Text);
}