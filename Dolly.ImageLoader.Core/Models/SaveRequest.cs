using Dolly.ImageLoader.Core.Enums;

namespace Dolly.ImageLoader.Core.Models;

public sealed class SaveRequest
{
    public ProductContext ProductContext { get; init; } = null!;
    public TargetMode TargetMode { get; init; }
    public IReadOnlyList<ImageSlotState> Slots { get; init; } = Array.Empty<ImageSlotState>();
    public string Username { get; init; } = string.Empty;
}