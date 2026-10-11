namespace SFallConfigurator.Options
{
	using System;
	using Configuration;
	using Infrastructure;
	using Localization;

	/// <summary>
	/// A single setting shown in the user interface and stored in an ini file.
	/// <para>
	/// An option is unavailable when its key is missing from the file; the user interface then shows it disabled.
	/// Changes are written to the in-memory document at once; the files are written when the program saves.
	/// </para>
	/// </summary>
	internal abstract class ConfigOption : ObservableObject
	{
		private readonly IConfigSource _source;
		private readonly String _label;
		private readonly String? _toolTip;

		private Func<Boolean>? _enableCondition;
		private Boolean _isAvailable;
		private Boolean _isModified;
		private Boolean _hasBaseline;
		private Object? _baseline;

		protected ConfigOption(OptionLocation location, String key, String label)
		{
			_source = location.Source;
			Section = location.Section;
			Key = key;
			_label = label;
		}

		public String Label => Localizer.T(_label);

		/// <summary>
		/// Explanation shown when the pointer rests on the option.
		/// </summary>
		public String? ToolTip
		{
			get => _toolTip is null ? null : Localizer.T(_toolTip);
			init => _toolTip = value;
		}

		public String? Section { get; }

		public String Key { get; }

		/// <summary>
		/// Raw value used when the key is missing. An option with a default is always available.
		/// </summary>
		public String? DefaultValue { get; init; }

		/// <summary>
		/// Alternative place to read the value from, used when the key is missing in the source.
		/// </summary>
		public Func<String?>? ReadFallback { get; init; }

		/// <summary>
		/// Extra requirement for the option to be available, checked each time it is loaded.
		/// </summary>
		public Func<Boolean>? AvailabilityCondition { get; init; }

		/// <summary>
		/// Called with the raw value after it was stored, to mirror it somewhere else.
		/// </summary>
		public Action<String>? Written { get; init; }

		/// <summary>
		/// True when the key exists in the configuration.
		/// </summary>
		public Boolean IsAvailable
		{
			get => _isAvailable;
			private set
			{
				if (SetProperty(ref _isAvailable, value))
				{
					OnPropertyChanged(nameof(IsEnabled));
				}
			}
		}

		/// <summary>
		/// True when the user can change the option: it is available and its dependencies allow it.
		/// </summary>
		public Boolean IsEnabled => _isAvailable && (_enableCondition?.Invoke() ?? true);

		/// <summary>
		/// True when the value differs from the one read when the program started.
		/// </summary>
		public Boolean IsModified
		{
			get => _isModified;
			private set => SetProperty(ref _isModified, value);
		}

		/// <summary>
		/// True while the value is being read from the configuration: a change then is not made by the user.
		/// </summary>
		public Boolean IsLoading { get; private set; }

		/// <summary>
		/// Makes the option depend on other options: it is enabled only while <paramref name="condition"/> holds.
		/// </summary>
		public ConfigOption EnableWhen(Func<Boolean> condition, params ConfigOption[] dependencies)
		{
			_enableCondition = condition;
			foreach (ConfigOption dependency in dependencies)
			{
				dependency.PropertyChanged += (_, _) => OnPropertyChanged(nameof(IsEnabled));
			}

			OnPropertyChanged(nameof(IsEnabled));
			return this;
		}

		/// <summary>
		/// Reads the value from the configuration. Does not write anything.
		/// </summary>
		public void Load()
		{
			IsLoading = true;
			try
			{
				String? raw = _source.Read(Section, Key) ?? ReadFallback?.Invoke();
				if (raw is not null && AvailabilityCondition is not null && !AvailabilityCondition())
				{
					raw = null;
				}

				raw ??= DefaultValue;
				IsAvailable = raw is not null;
				ApplyValue(raw);
			}
			finally
			{
				IsLoading = false;
			}

			if (!_hasBaseline)
			{
				_baseline = GetSnapshot();
				_hasBaseline = true;
			}

			UpdateModified();
			OnPropertyChanged(nameof(IsEnabled));
		}

		/// <summary>
		/// Reads another key of the same file than the option's own key.
		/// </summary>
		protected String? ReadOtherKey(String key)
		{
			return _source.Read(Section, key);
		}

		/// <summary>
		/// Stores another key of the same file than the option's own key.
		/// </summary>
		protected void WriteOtherKey(String key, String raw)
		{
			if (!IsLoading && _isAvailable)
			{
				_source.Write(Section, key, raw);
			}
		}

		/// <summary>
		/// Sets the displayed value from the raw ini text. <paramref name="raw"/> is null when unavailable.
		/// </summary>
		protected abstract void ApplyValue(String? raw);

		/// <summary>
		/// The current value in a form that can be compared with the baseline.
		/// </summary>
		protected abstract Object? GetSnapshot();

		/// <summary>
		/// To be called after the value changed: stores <paramref name="raw"/> unless the change comes from loading.
		/// </summary>
		protected void OnValueChanged(String? raw)
		{
			if (raw is not null && !IsLoading && _isAvailable)
			{
				_source.Write(Section, Key, raw);
				Written?.Invoke(raw);
			}

			UpdateModified();
		}

		private void UpdateModified()
		{
			IsModified = _hasBaseline && !Equals(_baseline, GetSnapshot());
		}
	}
}