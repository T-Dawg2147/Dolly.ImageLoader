namespace Dolly.ImageLoader.Core.Models;

public sealed class ProductContext
{
    public string ProductId { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string ParentGroupId { get; init; } = string.Empty;
    public string BaseCode { get; init; } = string.Empty;
    public string SupplierProductCode { get; init; } = string.Empty;
    public string ObjectType { get; init; } = string.Empty;
}