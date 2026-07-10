using Dolly.ImageLoader.Core.Models;

namespace Dolly.ImageLoader.Wpf.ViewModels;

public sealed class ProductSearchResultViewModel
{
    public ProductContext Product { get; init; } = new();

    public string ProductId => Product.ProductId;
    public string ProductName => Product.ProductName;
    public string ParentGroupId => Product.ParentGroupId;
    public string BaseCode => Product.BaseCode;

    public string TitleText => $"{ProductId}";

    public string DetailText =>
        $"Group: {ParentGroupId}    Base Code: {BaseCode}    Name: {ProductName}";
}