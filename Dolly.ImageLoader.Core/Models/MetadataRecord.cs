namespace Dolly.ImageLoader.Core.Models;

public sealed class MetadataRecord
{
    public string FileName { get; init; } = string.Empty;
    public string Product { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public string HierarchyClassification { get; init; } = string.Empty;
}