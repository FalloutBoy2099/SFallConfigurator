namespace SFallConfigurator.Mods
{
	using System;
	using Infrastructure;

	/// <summary>
	/// A mod of the load order list (mods_order.ini).
	/// </summary>
	internal sealed class ModEntry : ObservableObject
	{
		private Boolean _isEnabled;

		public ModEntry(String name, Boolean isEnabled, Boolean isMissing, String comment = "")
		{
			Name = name;
			_isEnabled = isEnabled;
			IsMissing = isMissing;
			Comment = comment;
		}

		public String Name { get; }

		/// <summary>
		/// True when neither a file nor a folder with this name exists in the mods folder.
		/// </summary>
		public Boolean IsMissing { get; }

		/// <summary>
		/// A trailing comment of the line in the order file, kept when the file is written.
		/// </summary>
		public String Comment { get; }

		/// <summary>
		/// The enabled flag of the order file. It is preserved for missing mods.
		/// </summary>
		public Boolean IsEnabled
		{
			get => _isEnabled;
			set
			{
				if (SetProperty(ref _isEnabled, value))
				{
					OnPropertyChanged(nameof(CheckState));
				}
			}
		}

		/// <summary>
		/// Check box state: indeterminate (null) for a missing mod.
		/// </summary>
		public Boolean? CheckState
		{
			get => IsMissing ? null : _isEnabled;
			set
			{
				if (!IsMissing && value.HasValue)
				{
					IsEnabled = value.Value;
				}
			}
		}
	}
}