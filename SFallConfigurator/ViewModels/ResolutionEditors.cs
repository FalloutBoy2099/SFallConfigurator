namespace SFallConfigurator.ViewModels
{
	using System;
	using System.ComponentModel;
	using Infrastructure;
	using Localization;
	using Options;
	using Services;

	/// <summary>
	/// Resolution settings of the Hi-Res patch (f2_res.ini): the size of the game screen.
	/// </summary>
	internal sealed class HiResResolutionEditor : ObservableObject
	{
		private const Int32 MinimumWidth = 640;
		private const Int32 MinimumHeight = 480;

		private Boolean _isUpdating;

		public HiResResolutionEditor(NumberOption width, NumberOption height, BoolOption scaleX2)
		{
			Width = width;
			Height = height;
			ScaleX2 = scaleX2;

			Width.PropertyChanged += OnSizeChanged;
			Height.PropertyChanged += OnSizeChanged;
			ScaleX2.PropertyChanged += OnScaleChanged;
			Resolutions.UserSelected += (_, mode) => SetSize(mode.Width, mode.Height);
		}

		public NumberOption Width { get; }

		public NumberOption Height { get; }

		/// <summary>
		/// The Hi-Res patch renders at half size and stretches: only larger modes make sense.
		/// </summary>
		public BoolOption ScaleX2 { get; }

		public ResolutionList Resolutions { get; } = new ResolutionList();

		/// <summary>
		/// Rebuilds the list after the options were loaded.
		/// </summary>
		public void Refresh()
		{
			Resolutions.Refresh(DisplayModes.GetAvailable(ScaleX2.Value == true), Width.Value, Height.Value);
		}

		/// <summary>
		/// Sets the game screen size (from the sfall resolution settings).
		/// </summary>
		public void SetResolution(Int32 width, Int32 height)
		{
			if (width == 0 || height == 0)
			{
				return;
			}

			SetSize(Math.Max(width, MinimumWidth), Math.Max(height, MinimumHeight));
			Resolutions.Select(Width.Value, Height.Value);
		}

		private void SetSize(Int32 width, Int32 height)
		{
			_isUpdating = true;
			try
			{
				Width.Value = width;
				Height.Value = height;
			}
			finally
			{
				_isUpdating = false;
			}
		}

		private void OnSizeChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(NumberOption.Value) && !_isUpdating)
			{
				Resolutions.Select(Width.Value, Height.Value);
			}
		}

		private void OnScaleChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName != nameof(BoolOption.Value) || ScaleX2.IsLoading)
			{
				return;
			}

			Refresh();

			// In scaled mode a size has to come from the list: take the smallest one that fits.
			if (ScaleX2.Value == true && Resolutions.Selected is null && Resolutions.Items.Count > 0)
			{
				ScreenResolution smallest = Resolutions.Items[^1];
				SetSize(smallest.Width, smallest.Height);
				Resolutions.Select(smallest.Width, smallest.Height);
			}
		}
	}

	/// <summary>
	/// Resolution settings of sfall (GraphicsWidth and GraphicsHeight). The Hi-Res patch can follow them.
	/// </summary>
	internal sealed class SfallResolutionEditor : ObservableObject
	{
		private readonly HiResResolutionEditor? _hiRes;

		private Boolean _isUpdating;
		private Boolean _setHiResResolution = true;
		private Boolean _scaleX2;
		private Boolean _isScaleAvailable;

		public SfallResolutionEditor(NumberOption width, NumberOption height, HiResResolutionEditor? hiRes)
		{
			Width = width;
			Height = height;
			_hiRes = hiRes;

			Width.PropertyChanged += OnSizeChanged;
			Height.PropertyChanged += OnSizeChanged;
			Resolutions.UserSelected += (_, mode) =>
			{
				_isUpdating = true;
				try
				{
					Width.Value = mode.Width;
					Height.Value = mode.Height;
				}
				finally
				{
					_isUpdating = false;
				}

				SynchronizeHiRes();
			};
		}

		public NumberOption Width { get; }

		public NumberOption Height { get; }

		public ResolutionList Resolutions { get; } = new ResolutionList();

		/// <summary>
		/// The Hi-Res patch has a configuration file, so its resolution can follow sfall's.
		/// </summary>
		public Boolean IsHiResAvailable => _hiRes is not null;

		/// <summary>
		/// Scaling needs one of the sfall graphics modes (the "Off" mode leaves the graphics to the Hi-Res patch).
		/// </summary>
		public Boolean IsScaleAvailable
		{
			get => _isScaleAvailable;
			set => SetProperty(ref _isScaleAvailable, value);
		}

		/// <summary>
		/// True when the Hi-Res patch resolution is set to the same value as sfall's.
		/// </summary>
		public Boolean SetHiResResolution
		{
			get => _setHiResResolution;
			set => SetProperty(ref _setHiResResolution, value);
		}

		/// <summary>
		/// sfall scales the picture of the Hi-Res patch to double size: the patch gets half the resolution.
		/// </summary>
		public Boolean ScaleX2
		{
			get => _scaleX2;
			set
			{
				if (!SetProperty(ref _scaleX2, value) || _isUpdating)
				{
					return;
				}

				Resolutions.Refresh(DisplayModes.GetAvailable(value), Width.Value, Height.Value);
				OnPropertyChanged(nameof(NeedsResolution));

				if (!value)
				{
					// Back to the normal size: the patch uses the full resolution again.
					_hiRes?.SetResolution(Width.Value, Height.Value);
				}
			}
		}

		/// <summary>
		/// True while scaling is on and no mode of the list is selected: the user must pick one.
		/// </summary>
		public Boolean NeedsResolution => _scaleX2 && Resolutions.Selected is null;

		public String Hint => Localizer.T("Now choose the required resolution.");

		/// <summary>
		/// Rebuilds the list after the options were loaded and detects whether scaling was in use.
		/// </summary>
		public void Refresh()
		{
			_isUpdating = true;
			try
			{
				ScaleX2 = _hiRes is not null
					&& _hiRes.Width.Value * 2 == Width.Value
					&& _hiRes.Height.Value * 2 == Height.Value;
			}
			finally
			{
				_isUpdating = false;
			}

			Resolutions.Refresh(DisplayModes.GetAvailable(_scaleX2), Width.Value, Height.Value);
			OnPropertyChanged(nameof(NeedsResolution));
		}

		private void OnSizeChanged(Object? sender, PropertyChangedEventArgs e)
		{
			// Reading the configuration is not an edit: the Hi-Res size must keep what its own file says.
			if (e.PropertyName != nameof(NumberOption.Value) || _isUpdating || ((ConfigOption)sender!).IsLoading)
			{
				return;
			}

			Resolutions.Select(Width.Value, Height.Value);
			OnPropertyChanged(nameof(NeedsResolution));
			SynchronizeHiRes();
		}

		private void SynchronizeHiRes()
		{
			OnPropertyChanged(nameof(NeedsResolution));
			if (!_setHiResResolution || _hiRes is null)
			{
				return;
			}

			Int32 width = Width.Value;
			Int32 height = Height.Value;
			if (_scaleX2)
			{
				width /= 2;
				height /= 2;
			}

			_hiRes.SetResolution(width, height);
		}
	}
}