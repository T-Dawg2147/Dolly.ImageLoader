namespace Dolly.ImageLoader.Core.Interfaces;

public interface IBadImageRepository
{
    Task<IReadOnlySet<string>> GetBadReferenceTypesAsync(
        string productOrGroupId,
        string productName,
        bool isProductMode,
        CancellationToken cancellationToken);
}