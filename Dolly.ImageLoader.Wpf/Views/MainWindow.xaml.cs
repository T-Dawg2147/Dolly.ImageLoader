using System.Windows;
using System.Windows.Input;
using Dolly.ImageLoader.Wpf.ViewModels;

namespace Dolly.ImageLoader.Wpf.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
    
    private void SearchResultsPopup_LostKeyboardFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.CloseSearchResultsCommand.Execute(null);
        }
    }

    private void SearchTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        if (DataContext is not MainWindowViewModel vm)
            return;

        if (vm.SearchCommand.CanExecute(null))
        {
            vm.SearchCommand.Execute(null);
            e.Handled = true;
        }
    }
}