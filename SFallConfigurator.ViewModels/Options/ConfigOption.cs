namespace SFallConfigurator.Options
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using System.Windows.Input;
	using Configuration;
	using Infrastructure;
	using Localization;

	/// <summary>
	/// How the changes of an option are grouped in its history: the changes that follow each other within the window count as one,
	/// so that dragging a slider is a single step to undo.
	/// </summary>
	internal static class OptionHistory
	{
		public static Func<Int64> Clock { get; set; } = () => Environment.TickCount64;

		public static Int64 CoalesceMilliseconds { get; set; } = 600;
	}

	/// <summary>
	/// A single setting shown in the user interface and stored in an ini file.
	/// <para>
	/// An option is unavailable when its key is missing from the file; the user interface then shows it disabled.
	/// Changes are written to the in-memory document at once; the files are written when the program saves.
	/// </para>
	/// </summary>
	internal abstract class ConfigOption : ObservableObject
	{
		private const Int64 NoChange = -1_000_000_000_000;
		private const Int32 HighlightMilliseconds = 2500;

		private readonly IConfigSource _source;
		private readonly String _label;
		private String? _toolTip;

		private Func<Boolean>? _enableCondition;
		private Boolean _isAvailable;
		private Boolean _isModified;
		private Boolean _hasBaseline;
		private Object? _baseline;

		private readonly Stack<String> _undoStack = new();
		private readonly Stack<String> _redoStack = new();
		private String? _current;
		private String? _loadedRaw;
		private Boolean _hasLoadedRaw;
		private Boolean _isRestoring;
		private Boolean _isHighlighted;
		private Int32 _highlightVersion;
		private Int64 _lastChange = NoChange;

		protected ConfigOption(OptionLocation location, String key, String label)
		{
			_source = location.Source;
			Section = location.Section;
			Key = key;
			_label = label;

			UndoCommand = new RelayCommand(Undo, () => CanUndo);
			RedoCommand = new RelayCommand(Redo, () => CanRedo);
			ResetCommand = new RelayCommand(ResetToDefault, () => CanResetToDefault);
			RevertCommand = new RelayCommand(Revert, () => CanRevert);
			AddKeyCommand = new RelayCommand(AddKey, () => CanAddKey);
		}

		/// <summary>
		/// The name of the option in its page of the catalog (null for an option that the catalog does not describe).
		/// </summary>
		public String? Id { get; set; }

		public String Label => Localizer.Label(_label);

		/// <summary>
		/// The label on one line, for the lists (the search, the preview of the changes).
		/// </summary>
		public String PlainLabel => Label.Replace('\n', ' ');

		/// <summary>
		/// The label as it is written in the sources and in the catalog: the key of the translations.
		/// </summary>
		internal String RawLabel => _label;

		/// <summary>
		/// The explanation as it is written in the sources and in the catalog: the key of the translations.
		/// </summary>
		internal String? RawToolTip => _toolTip;

		/// <summary>
		/// The file of the option.
		/// </summary>
		internal IConfigSource Source => _source;

		/// <summary>
		/// Explanation shown when the pointer rests on the option.
		/// </summary>
		public String? ToolTip
		{
			get => _toolTip is null ? null : Localizer.T(_toolTip);
			set => _toolTip = value;
		}

		public String? Section { get; }

		public String Key { get; }

		/// <summary>
		/// Where the option is stored, as the catalog says it (sfall, sfallMods, hires, gameCfg, translations), independent of the
		/// names of the files of a game folder.
		/// </summary>
		public String? Origin { get; set; }

		/// <summary>
		/// What identifies the option in a profile, whatever the game folder; null for an option that a profile does not keep.
		/// </summary>
		public String? ProfileKey => Origin is null || Id is null || !IsKeptInProfiles ? null : Origin + "|" + Section + "|" + Key;

		/// <summary>
		/// False for the options that belong to one game folder (paths, checksums), which a profile must not carry to another.
		/// </summary>
		protected virtual Boolean IsKeptInProfiles => true;

		/// <summary>
		/// The raw value of the option as the file holds it now; null while the option is unavailable.
		/// </summary>
		public String? CurrentRaw => _current;

		/// <summary>
		/// The name of the file that stores the option, for the help text.
		/// </summary>
		public String? FileName { get; set; }

		/// <summary>
		/// The raw value that the program (sfall, the Hi-Res patch, the game) uses when the key is not set, when it is known.
		/// </summary>
		public String? KnownDefault { get; set; }

		/// <summary>
		/// True for a moment after a search took the user to the option.
		/// </summary>
		public Boolean IsHighlighted
		{
			get => _isHighlighted;
			private set => SetProperty(ref _isHighlighted, value);
		}

		public ICommand UndoCommand { get; }

		public ICommand RedoCommand { get; }

		public ICommand ResetCommand { get; }

		public ICommand RevertCommand { get; }

		public ICommand AddKeyCommand { get; }

		/// <summary>
		/// The section where the key is added when the file lacks it. Several sections, separated by "|", are the places that
		/// different versions of the file use: the first one that the file has is taken, else the first of the list. Without it the
		/// section of the option is used; an option that has neither cannot be added.
		/// </summary>
		public String? AddToSection { get; set; }

		/// <summary>
		/// True when the key is not in the file and can be added to it.
		/// </summary>
		public Boolean CanAddKey => !_isAvailable && _source.CanAdd && GetSectionForNewKey() is not null && _source.Read(Section, Key) is null && ReadFallback?.Invoke() is null;

		public Boolean CanUndo => _isAvailable && _undoStack.Count > 0;

		public Boolean CanRedo => _isAvailable && _redoStack.Count > 0;

		public Boolean HasDefault => KnownDefault is not null;

		/// <summary>
		/// True when the value is the default one, or when the default is not known.
		/// </summary>
		public Boolean IsDefault => KnownDefault is null || _current is null || DescribeRaw(_current) == DescribeRaw(KnownDefault);

		public Boolean CanResetToDefault => _isAvailable && !IsDefault;

		/// <summary>
		/// True when the saved value (the one read when the program started) can be put back.
		/// </summary>
		public Boolean CanRevert => _isAvailable && IsModified;

		/// <summary>
		/// The default value as the user sees it, or null when it is not known.
		/// </summary>
		public String? DefaultText => KnownDefault is null ? null : DescribeRaw(KnownDefault);

		/// <summary>
		/// The text of the help: the explanation, the default value and where the option is stored.
		/// </summary>
		public String HelpText
		{
			get
			{
				StringBuilder text = new();
				if (_toolTip is not null)
				{
					text.Append(ToolTip);
				}

				if (DefaultText is not null)
				{
					text.Append(text.Length > 0 ? "\n\n" : String.Empty).Append(Localizer.F("Default: {0}", DefaultText));
				}

				if (CanAddKey)
				{
					text.Append(text.Length > 0 ? "\n\n" : String.Empty).Append(Localizer.T("This key is not in the file: right-click the option to add it."));
				}

				String location = Section is null ? Key : "[" + Section + "] " + Key;
				text.Append(text.Length == 0 ? String.Empty : DefaultText is null ? "\n\n" : "\n").Append(Localizer.F("Stored in: {0}", FileName is null ? location : FileName + " " + location));
				return text.ToString();
			}
		}

		/// <summary>
		/// Raw value used when the key is missing. An option with a default is always available.
		/// </summary>
		public String? DefaultValue { get; set; }

		/// <summary>
		/// Alternative place to read the value from, used when the key is missing in the source.
		/// </summary>
		public Func<String?>? ReadFallback { get; set; }

		/// <summary>
		/// Extra requirement for the option to be available, checked each time it is loaded.
		/// </summary>
		public Func<Boolean>? AvailabilityCondition { get; set; }

		/// <summary>
		/// Called with the raw value after it was stored, to mirror it somewhere else.
		/// </summary>
		public Action<String>? Written { get; set; }

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
				_current = raw;
				_undoStack.Clear();
				_redoStack.Clear();
				if (!_hasLoadedRaw)
				{
					_loadedRaw = raw;
					_hasLoadedRaw = true;
				}
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
			NotifyHistoryChanged();
		}

		/// <summary>
		/// Marks the option for a moment, so that the user finds it on the page.
		/// </summary>
		public void Highlight()
		{
			SynchronizationContext? context = SynchronizationContext.Current;
			Int32 version = Interlocked.Increment(ref _highlightVersion);
			IsHighlighted = true;

			// A second search for the same option keeps the mark for a full time again: only the last one ends it.
			_ = Task.Delay(HighlightMilliseconds).ContinueWith(
				_ =>
				{
					if (version != Volatile.Read(ref _highlightVersion))
					{
						return;
					}

					if (context is null)
					{
						IsHighlighted = false;
					}
					else
					{
						context.Post(_ => IsHighlighted = false, null);
					}
				},
				TaskScheduler.Default);
		}

		/// <summary>
		/// Adds the key to the file, with the value that the option has before anything is chosen, and shows it as an available option.
		/// </summary>
		public void AddKey()
		{
			String? section = GetSectionForNewKey();
			if (section is null || !CanAddKey || !_source.Add(section, Key, InitialRaw))
			{
				return;
			}

			Load();
		}

		/// <summary>
		/// The value written when the key is added: the default when it is known.
		/// </summary>
		internal virtual String InitialRaw => KnownDefault ?? DefaultValue ?? "0";

		private String? GetSectionForNewKey()
		{
			String[] candidates = (AddToSection ?? Section)?.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
			return candidates.FirstOrDefault(_source.HasSection) ?? candidates.FirstOrDefault();
		}

		/// <summary>
		/// Puts back the value that preceded the last change.
		/// </summary>
		public void Undo()
		{
			if (CanUndo)
			{
				_redoStack.Push(_current!);
				Restore(_undoStack.Pop(), record: false);
			}
		}

		/// <summary>
		/// Puts back the value that the last undo took away.
		/// </summary>
		public void Redo()
		{
			if (CanRedo)
			{
				_undoStack.Push(_current!);
				Restore(_redoStack.Pop(), record: false);
			}
		}

		/// <summary>
		/// Gives the option a value, as if the user had chosen it. The change can be undone. Returns true when the value changed.
		/// </summary>
		public Boolean ApplyRaw(String raw)
		{
			if (!_isAvailable || _current is null || DescribeRaw(raw) == DescribeRaw(_current))
			{
				return false;
			}

			Restore(raw, record: true);
			return true;
		}

		/// <summary>
		/// Sets the default value. The change can be undone.
		/// </summary>
		public void ResetToDefault()
		{
			if (KnownDefault is not null && CanResetToDefault)
			{
				Restore(KnownDefault, record: true);
			}
		}

		/// <summary>
		/// Sets the value that was read when the program started. The change can be undone.
		/// </summary>
		public void Revert()
		{
			if (_loadedRaw is not null && CanRevert)
			{
				Restore(_loadedRaw, record: true);
			}
		}

		/// <summary>
		/// The text that shows a raw value to the user (the default value in the help, for example).
		/// </summary>
		internal virtual String DescribeRaw(String raw)
		{
			return raw;
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
				if (!_isRestoring)
				{
					Record(raw);
				}
			}

			UpdateModified();
			NotifyHistoryChanged();
		}

		/// <summary>
		/// Shows <paramref name="raw"/> and stores it as the value of the key, whatever the option does with it.
		/// </summary>
		private void Restore(String raw, Boolean record)
		{
			_isRestoring = true;
			try
			{
				ApplyValue(raw);
			}
			finally
			{
				_isRestoring = false;
			}

			_source.Write(Section, Key, raw);
			Written?.Invoke(raw);
			if (record)
			{
				Record(raw);
			}
			else
			{
				_current = raw;
				_lastChange = NoChange;
			}

			UpdateModified();
			NotifyHistoryChanged();
		}

		private void Record(String raw)
		{
			if (_current is null || raw == _current)
			{
				_current = raw;
				return;
			}

			Int64 now = OptionHistory.Clock();
			if (_undoStack.Count == 0 || now - _lastChange > OptionHistory.CoalesceMilliseconds)
			{
				_undoStack.Push(_current);
			}

			_lastChange = now;
			_redoStack.Clear();
			_current = raw;
		}

		private void NotifyHistoryChanged()
		{
			OnPropertyChanged(nameof(CanUndo));
			OnPropertyChanged(nameof(CanRedo));
			OnPropertyChanged(nameof(IsDefault));
			OnPropertyChanged(nameof(CanResetToDefault));
			OnPropertyChanged(nameof(CanRevert));
			((RelayCommand)UndoCommand).RaiseCanExecuteChanged();
			((RelayCommand)RedoCommand).RaiseCanExecuteChanged();
			((RelayCommand)ResetCommand).RaiseCanExecuteChanged();
			((RelayCommand)RevertCommand).RaiseCanExecuteChanged();
		}

		private void UpdateModified()
		{
			IsModified = _hasBaseline && !Equals(_baseline, GetSnapshot());
		}
	}
}