namespace Dolly.ImageLoader.Core.Models;

public sealed class AssetNamePlan
{
    public string TargetId { get; init; } = string.Empty;
    public string BaseImageName { get; init; } = string.Empty;
    public string ReferenceType { get; init; } = string.Empty;

    public string PrimaryAssetName { get; init; } = string.Empty;
    public string? CutOutAssetName { get; init; }

    public string HierarchyClassification { get; init; } = string.Empty;
    public string SectionIdentifier { get; init; } = string.Empty;

    public IReadOnlyList<string> OutputFileNames { get; init; } = Array.Empty<string>();
}