namespace SFallConfigurator.Views
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using Mods;
	using ViewModels;

	/// <summary>
	/// The Mods tab. The load order can be changed by dragging the mods.
	/// </summary>
	public partial class ModsView : UserControl
	{
		private Point _dragStart;
		private ModEntry? _dragItem;

		public ModsView()
		{
			InitializeComponent();
		}

		private static ModEntry? GetModAt(ListBox list, Object originalSource)
		{
			return originalSource is DependencyObject source
				&& ItemsControl.ContainerFromElement(list, source) is ListBoxItem container
				? container.DataContext as ModEntry
				: null;
		}

		private void OnOrderPreviewMouseLeftButtonDown(Object sender, MouseButtonEventArgs e)
		{
			_dragStart = e.GetPosition(null);
			_dragItem = GetModAt(OrderList, e.OriginalSource);
		}

		private void OnOrderPreviewMouseMove(Object sender, MouseEventArgs e)
		{
			if (_dragItem is null || e.LeftButton != MouseButtonState.Pressed)
			{
				return;
			}

			Vector moved = e.GetPosition(null) - _dragStart;
			if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance
				&& Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
			{
				return;
			}

			ModEntry dragged = _dragItem;
			_dragItem = null;
			DragDrop.DoDragDrop(OrderList, new DataObject(typeof(ModEntry), dragged), DragDropEffects.Move);
		}

		private void OnOrderDragOver(Object sender, DragEventArgs e)
		{
			e.Effects = e.Data.GetDataPresent(typeof(ModEntry)) ? DragDropEffects.Move : DragDropEffects.None;
			e.Handled = true;
		}

		private void OnOrderDrop(Object sender, DragEventArgs e)
		{
			if (DataContext is not ModsViewModel viewModel || e.Data.GetData(typeof(ModEntry)) is not ModEntry dragged)
			{
				return;
			}

			// Dropping below the last mod moves the dragged mod to the end.
			ModEntry? target = GetModAt(OrderList, e.OriginalSource);
			Int32 targetIndex = target is null ? viewModel.Mods.Count - 1 : viewModel.Mods.IndexOf(target);
			viewModel.MoveMod(viewModel.Mods.IndexOf(dragged), targetIndex);
			e.Handled = true;
		}
	}
}