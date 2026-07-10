using Dolly.ImageLoader.Core.Enums;
using Dolly.ImageLoader.Core.Models;

namespace Dolly.ImageLoader.Core.Interfaces;

public interface IAmazonImageService
{
    AmazonImagePlan BuildPlan(
        ProductContext productContext,
        TargetMode targetMode,
        ImageSlotDefinition slotDefinition);

    Task WriteImagesAsync(
        string sourceFilePath,
        AmazonImagePlan plan,
        CancellationToken cancellationToken);
}