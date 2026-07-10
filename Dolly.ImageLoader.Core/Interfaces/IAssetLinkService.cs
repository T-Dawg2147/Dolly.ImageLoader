namespace Dolly.ImageLoader.Core.Interfaces;

public interface IAssetLinkService
{
    Task LinkAsync(string id, string imageName, string referenceType, string username, CancellationToken ct);
    Task UnlinkAsync(string id, string imageName, string referenceType, string userName, CancellationToken ct);
}