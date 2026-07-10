namespace Dolly.ImageLoader.Core.Options;

public sealed class PathOptions
{
    public string PreviewImageRoot { get; set; } = string.Empty;
    public string HotfolderRoot { get; set; } = string.Empty;
    public string WorkingRoot { get; set; } = string.Empty;
    public string FallbackPreviewPrimaryBaseUrl { get; set; } = string.Empty;
    public string FallbackPreviewAltBaseUrl { get; set; } = string.Empty;

    public string AmazonImportRoot { get; set; } = string.Empty;

    
    public string AmazonPrimaryLargeRoot { get; set; } = string.Empty;
    public string AmazonPrimaryMediumRoot { get; set; } = string.Empty;
    public string AmazonPrimarySmallRoot { get; set; } = string.Empty;

    public string AmazonSecondaryLargeRoot { get; set; } = string.Empty;
    public string AmazonSecondaryMediumRoot { get; set; } = string.Empty;
    public string AmazonSecondarySmallRoot { get; set; } = string.Empty;
}