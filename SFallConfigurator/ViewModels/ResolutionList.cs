namespace SFallConfigurator.ViewModels
{
	using System;
	using System.Collections.ObjectModel;
	using System.Collections.Generic;
	using Infrastructure;
	using Services;

	/// <summary>
	/// The list of display modes offered to the user, with the selected one.
	/// </summary>
	internal sealed class ResolutionList : ObservableObject
	{
		private ScreenResolution? _selected;
		private Boolean _isUpdating;

		public ObservableCollection<ScreenResolution> Items { get; } = [];

		/// <summary>
		/// Raised only when the user picks a mode, not when the selection is set by the program.
		/// </summary>
		public event EventHandler<ScreenResolution>? UserSelected;

		public ScreenResolution? Selected
		{
			get => _selected;
			set
			{
				if (_selected == value)
				{
					return;
				}

				_selected = value;
				OnPropertyChanged();

				if (!_isUpdating && value.HasValue)
				{
					UserSelected?.Invoke(this, value.Value);
				}
			}
		}

		/// <summary>
		/// Fills the list and selects the mode of the given size, if it is in the list.
		/// </summary>
		public void Refresh(IEnumerable<ScreenResolution> modes, Int32 width, Int32 height)
		{
			_isUpdating = true;
			try
			{
				Items.Clear();
				foreach (ScreenResolution mode in modes)
				{
					Items.Add(mode);
				}
			}
			finally
			{
				_isUpdating = false;
			}

			Select(width, height);
		}

		/// <summary>
		/// Selects the mode of the given size without raising <see cref="UserSelected"/>.
		/// </summary>
		public void Select(Int32 width, Int32 height)
		{
			_isUpdating = true;
			try
			{
				Selected = Items.Contains(new ScreenResolution(width, height)) ? new ScreenResolution(width, height) : null;
			}
			finally
			{
				_isUpdating = false;
			}
		}
	}
}