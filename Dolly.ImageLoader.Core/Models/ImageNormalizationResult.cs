namespace Dolly.ImageLoader.Core.Models;

public sealed class ImageNormalizationResult
{
    public string OriginalFilePath { get; set; } = string.Empty;
    public string OutputFilePath { get; set; } = string.Empty;

    public bool WasNormalized { get; set; }
    public bool NeedsNormalization { get; set; }

    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
}