using Dolly.ImageLoader.Core.Enums;
using Dolly.ImageLoader.Core.Interfaces;
using Dolly.ImageLoader.Core.Models;

namespace Dolly.ImageLoader.Infrastructure.Services;

public sealed class AssetNamingService : IAssetNamingService
{
    public AssetNamePlan BuildPlan(
        ProductContext productContext,
        TargetMode targetMode,
        ImageSlotDefinition slotDefinition,
        ImageImportMetadata importMetadata)
    {
        var targetId = targetMode == TargetMode.Product
            ? productContext.ProductId
            : productContext.ParentGroupId;

        var suffix = targetMode == TargetMode.Product
            ? slotDefinition.ProductAssetSuffix
            : slotDefinition.GroupAssetSuffix;

        var primaryAssetName = $"{importMetadata.ImageName}{suffix}";
        var fileNames = new List<string> { $"{primaryAssetName}.jpg" };

        string? cutOutAssetName = null;
        if (slotDefinition.SupportsCutOut)
        {
            cutOutAssetName = $"{primaryAssetName}_CO";
            fileNames.Add($"{cutOutAssetName}.jpg");
        }

        return new AssetNamePlan
        {
            TargetId = targetId,
            BaseImageName = importMetadata.ImageName,
            ReferenceType = slotDefinition.ReferenceType,
            PrimaryAssetName = primaryAssetName,
            CutOutAssetName = cutOutAssetName,
            HierarchyClassification = importMetadata.HierarchyClassification,
            SectionIdentifier = importMetadata.SectionIdentifier,
            OutputFileNames = fileNames
        };
    }
}