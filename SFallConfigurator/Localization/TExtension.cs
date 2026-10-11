namespace SFallConfigurator.Localization
{
	using System;
	using System.Windows.Data;
	using System.Windows.Markup;

	/// <summary>
	/// XAML markup extension that shows a translated text: {loc:T 'Some text'}.
	/// </summary>
	[MarkupExtensionReturnType(typeof(String))]
	public sealed class TExtension : MarkupExtension
	{
		public TExtension(String text)
		{
			Text = text;
		}

		public String Text { get; set; }

		public override Object ProvideValue(IServiceProvider serviceProvider)
		{
			Binding binding = new Binding(nameof(LocalizedText.Value))
			{
				Source = Localizer.Instance.Observe(Text),
				Mode = BindingMode.OneWay,
			};

			return binding.ProvideValue(serviceProvider);
		}
	}
}