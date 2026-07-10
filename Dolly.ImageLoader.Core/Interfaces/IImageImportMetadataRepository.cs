using Dolly.ImageLoader.Core.Models;

namespace Dolly.ImageLoader.Core.Interfaces;

public interface IImageImportMetadataRepository
{
    Task<ImageImportMetadata?> GetByTargetIdAsync(string targetId, CancellationToken cancellationToken);
}