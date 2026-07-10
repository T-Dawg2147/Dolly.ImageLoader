using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Dolly.ImageLoader.Batch.ViewModels;
using Dolly.ImageLoader.Wpf.Shared.ViewModels;

namespace Dolly.ImageLoader.Batch.Views;

public partial class BatchLoaderWindow : Window
{
    private Point _dragStartPoint;

    public BatchLoaderWindow(BatchLoaderViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Closing += BatchLoaderWindow_Closing;

        if (viewModel is INotifyPropertyChanged inpc)
        {
            inpc.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void BatchLoaderWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (DataContext is not BatchLoaderViewModel vm)
        {
            return;
        }

        if (!vm.CanClose())
        {
            e.Cancel = true;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BatchLoaderViewModel.SelectedSlot))
        {
            UpdateSlotHighlighting();
        }
    }

    private void UpdateSlotHighlighting()
    {
        if (DataContext is not BatchLoaderViewModel vm)
        {
            return;
        }

        UpdateSlotHighlightingRecursive(this, vm.SelectedSlot);
    }

    private static void UpdateSlotHighlightingRecursive(DependencyObject parent, ImageSlotViewModel? selectedSlot)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is Border border && border.Tag is ImageSlotViewModel slot)
            {
                border.BorderBrush = ReferenceEquals(slot, selectedSlot)
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A90E2"))
                    : Brushes.Transparent;
            }

            UpdateSlotHighlightingRecursive(child, selectedSlot);
        }
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        await HandleFileDropAsync(e);
    }

    private async void ImagePool_Drop(object sender, DragEventArgs e)
    {
        await HandleFileDropAsync(e);
    }

    private async Task HandleFileDropAsync(DragEventArgs e)
    {
        if (DataContext is not BatchLoaderViewModel vm)
        {
            return;
        }

        if (vm.IsInteractionLocked)
        {
            e.Handled = true;
            return;
        }

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return;
        }

        e.Handled = true;
        await vm.AddImageFilesAsync(files);
    }

    private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
    {
        if (DataContext is not BatchLoaderViewModel vm || vm.IsInteractionLocked)
        {
            return;
        }

        if (e.OriginalSource is not TreeViewItem item)
        {
            return;
        }

        if (item.DataContext is BatchProductNodeViewModel node)
        {
            vm.SelectedTreeNode = node;
        }
    }

    private void SlotBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not BatchLoaderViewModel vm || vm.IsInteractionLocked)
        {
            return;
        }

        if (sender is not Border border)
        {
            return;
        }

        if (border.Tag is ImageSlotViewModel slot)
        {
            vm.SelectSlotCommand.Execute(slot);
            UpdateSlotHighlighting();
        }
    }

    private void ImagePoolListBox_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not BatchLoaderViewModel vm || vm.IsInteractionLocked)
        {
            return;
        }

        if (sender is not ListBox listBox)
        {
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            var currentPosition = e.GetPosition(null);

            if (Math.Abs(currentPosition.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(currentPosition.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (GetAncestor<ListBoxItem>((DependencyObject)e.OriginalSource) is { DataContext: BatchImagePoolItemViewModel item })
                {
                    var data = new DataObject(typeof(BatchImagePoolItemViewModel), item);
                    DragDrop.DoDragDrop(listBox, data, DragDropEffects.Copy);
                }
            }
        }
        else
        {
            _dragStartPoint = e.GetPosition(null);
        }
    }

    private async void SlotBorder_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not BatchLoaderViewModel vm || vm.IsInteractionLocked)
        {
            return;
        }

        if (sender is not Border border || border.Tag is not ImageSlotViewModel slot)
        {
            return;
        }

        vm.SelectSlotCommand.Execute(slot);
        UpdateSlotHighlighting();

        if (e.Data.GetDataPresent(typeof(BatchImagePoolItemViewModel)) &&
            e.Data.GetData(typeof(BatchImagePoolItemViewModel)) is BatchImagePoolItemViewModel item)
        {
            e.Handled = true;
            await vm.AssignPoolImageToSlotAsync(item, slot);
        }
    }

    private async void GroupSearchTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (DataContext is not BatchLoaderViewModel vm || vm.IsInteractionLocked)
            return;

        if (vm.LoadGroupCommand.CanExecute(null))
        {
            vm.LoadGroupCommand.Execute(null);
            e.Handled = true;
        }
    }

    private static T? GetAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T target)
            {
                return target;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
