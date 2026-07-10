namespace Dolly.ImageLoader.Core.Models;

public sealed class AmazonImagePlan
{
    public string ImportFileName { get; init; } = string.Empty;
    public string LargeFileName { get; init; } = string.Empty;
    public string MediumFileName { get; init; } = string.Empty;
    public string SmallFileName { get; init; } = string.Empty;

    public bool IsSecondary { get; init; }
}