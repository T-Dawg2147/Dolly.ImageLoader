using Dolly.ImageLoader.Core.Models;

namespace Dolly.ImageLoader.Core.Interfaces;

public interface IImageProcessingService
{
    Task<ImageValidationResult> ValidateImageAsync(string filePath, CancellationToken ct);
}