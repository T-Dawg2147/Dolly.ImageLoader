namespace Dolly.ImageLoader.Core.Interfaces;

public interface IAssetReferenceRepository
{
    Task<IDictionary<string, string?>> GetAssetReferencesAsync(string targetId, CancellationToken ct);
}