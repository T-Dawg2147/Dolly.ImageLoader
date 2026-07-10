namespace Dolly.ImageLoader.Core.Options;

public sealed class PackagingOptions
{
    public bool EnabledMetadataZip { get; set; } = true;
    public string MetadataFileName { get; set; } = "Metadata.csv";
}