using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Dolly.ImageLoader.Core.Models;

namespace Dolly.ImageLoader.Wpf.ViewModels;

public partial class BatchProductNodeViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private bool isGroup;

    [ObservableProperty]
    private ProductContext? product;

    [ObservableProperty]
    private bool hasPendingChanges;

    public ObservableCollection<BatchProductNodeViewModel> Children { get; } = new();

    public string Subtitle =>
        Product is null
            ? string.Empty
            : IsGroup
                ? $"Group target: {Product.ProductId}"
                : $"Group: {Product.ParentGroupId}";
}