using Dolly.ImageLoader.Core.Models;

namespace Dolly.ImageLoader.Core.Interfaces;

public interface IMetadataService
{
    Task<string> ExportMetadataCsvAsync(
        IReadOnlyList<MetadataRecord> records,
        string workingFolderPath,
        CancellationToken cancellationToken);
}