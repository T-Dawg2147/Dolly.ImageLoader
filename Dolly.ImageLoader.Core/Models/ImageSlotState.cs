using Dolly.ImageLoader.Core.Enums;

namespace Dolly.ImageLoader.Core.Models;

public sealed class ImageSlotState
{
    public ImageSlotDefinition Definition { get; init; } = null!;

    public string? CurrentAssetName { get; set; }
    public string? CurrentImagePath { get; set; }

    public string? PendingSourceFilePath { get; set; }
    public PendingActionType PendingAction { get; set; }

    public bool HasWarning { get; set; }
    public string? WarningMessage { get; set; }

    public IReadOnlyList<string> PlannedAssetNames { get; set; } = Array.Empty<string>();
}