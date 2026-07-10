namespace Dolly.ImageLoader.Core.Options;

public class ImageOptions
{
    public int MaxSecondarySlots { get; set; } = 9;
    public string[] AllowedExtensions { get; set; } = Array.Empty<string>();

    public int MinWidth { get; set; } = 300;
    public int MinHeight { get; set; } = 300;

    public int MaxWidth { get; set; } = 6000;
    public int MaxHeight { get; set; } = 6000;

    public int MinDpi { get; set; } = 72;

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}