using Dolly.ImageLoader.Core.Enums;
using Dolly.ImageLoader.Core.Models;

namespace Dolly.ImageLoader.Core.Interfaces;

public interface IAssetNamingService
{
    AssetNamePlan BuildPlan(
        ProductContext productContext,
        TargetMode targetMode,
        ImageSlotDefinition slotDefinition,
        ImageImportMetadata importMetadata);
}