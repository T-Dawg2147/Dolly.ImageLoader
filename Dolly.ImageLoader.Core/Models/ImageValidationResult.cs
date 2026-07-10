namespace Dolly.ImageLoader.Core.Models;

public class ImageValidationResult
{
    public bool IsValid { get; init; }
    public string Message { get; init; } = string.Empty;

    public bool HasWarning { get; set; }
    public string? WarningMessage { get; set; }

    public int Width { get; init; }
    public int Height { get; init; }

    public double HorizontalDpi { get; init; }
    public double VerticalDpi { get; init; }

    public long FileSizeBytes { get; init; }
}