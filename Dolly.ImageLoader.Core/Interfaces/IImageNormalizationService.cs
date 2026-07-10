using Dolly.ImageLoader.Core.Models;

namespace Dolly.ImageLoader.Core.Interfaces;

public interface IImageNormalizationService
{
    Task<IReadOnlyList<ImageNormalizationResult>> NormalizeIfNeededAsync(
        IEnumerable<string> filePaths,
        CancellationToken ct = default);

    Task<IReadOnlyList<ImageNormalizationResult>> InspectAsync(
        IEnumerable<string> filePaths,
        CancellationToken ct = default);
}