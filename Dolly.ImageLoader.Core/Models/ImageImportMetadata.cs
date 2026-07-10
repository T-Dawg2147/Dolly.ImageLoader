namespace Dolly.ImageLoader.Core.Models;

public sealed class ImageImportMetadata
{
    public string ImageName { get; init; } = string.Empty;
    public string HierarchyClassification { get; init; } = string.Empty;
    public string SectionIdentifier { get; init; } = string.Empty;
    public string? FolderPath { get; init; }
    public string? LegacyPath { get; init; }
}