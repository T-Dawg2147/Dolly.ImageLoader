using Dolly.ImageLoader.Core.Enums;

namespace Dolly.ImageLoader.Core.Models;

public sealed class ImageSlotDefinition
{
    public int SlotNumber { get; init; }
    public ImageSlotKind Kind { get; init; }
    public string DisplayLabel { get; init; } = string.Empty;

    public string ReferenceType { get; init; } = string.Empty;
    public bool SupportsCutOut { get; init; }

    public string ProductAssetSuffix { get; init; } = string.Empty;
    public string GroupAssetSuffix { get; init; } = string.Empty;
}