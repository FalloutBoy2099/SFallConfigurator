namespace SFallConfigurator.Views
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using Infrastructure;

	/// <summary>
	/// Shows a report: lines marked by how serious they are.
	/// </summary>
	public partial class ReportDialog : Window
	{
		internal ReportDialog(String title, IReadOnlyList<ReportLine> lines)
		{
			InitializeComponent();
			TitleBarTheme.Attach(this);

			Title = title;
			Lines.ItemsSource = lines;
		}
	}
}