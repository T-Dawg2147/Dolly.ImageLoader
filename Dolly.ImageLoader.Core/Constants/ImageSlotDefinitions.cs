using Dolly.ImageLoader.Core.Enums;
using Dolly.ImageLoader.Core.Models;

namespace Dolly.ImageLoader.Core.Constants;

public static class ImageSlotDefinitions
{
    public static IReadOnlyList<ImageSlotDefinition> All { get; } =
        new List<ImageSlotDefinition>
        {
            new()
            {
                SlotNumber = 0,
                Kind = ImageSlotKind.Primary,
                DisplayLabel = "Primary Image",
                ReferenceType = "Primary_Image",
                SupportsCutOut = true,
                ProductAssetSuffix = "_PI",
                GroupAssetSuffix = "_PG"
            },
            new()
            {
                SlotNumber = 1,
                Kind = ImageSlotKind.Secondary,
                DisplayLabel = "Secondary Image 1",
                ReferenceType = "Secondary_Image",
                SupportsCutOut = false,
                ProductAssetSuffix = "_SI_01",
                GroupAssetSuffix = "_PIMG_01"
            },
            new()
            {
                SlotNumber = 2,
                Kind = ImageSlotKind.Secondary,
                DisplayLabel = "Secondary Image 2",
                ReferenceType = "Secondary_Image_02",
                SupportsCutOut = false,
                ProductAssetSuffix = "_SI_02",
                GroupAssetSuffix = "_PIMG_02"
            },
            new()
            {
                SlotNumber = 3,
                Kind = ImageSlotKind.Secondary,
                DisplayLabel = "Secondary Image 3",
                ReferenceType = "Secondary_Image_03",
                SupportsCutOut = false,
                ProductAssetSuffix = "_SI_03",
                GroupAssetSuffix = "_PIMG_03"
            },
            new()
            {
                SlotNumber = 4,
                Kind = ImageSlotKind.Secondary,
                DisplayLabel = "Secondary Image 4",
                ReferenceType = "Secondary_Image_04",
                SupportsCutOut = false,
                ProductAssetSuffix = "_SI_04",
                GroupAssetSuffix = "_PIMG_04"
            },
            new()
            {
                SlotNumber = 5,
                Kind = ImageSlotKind.Secondary,
                DisplayLabel = "Secondary Image 5",
                ReferenceType = "Secondary_Image_05",
                SupportsCutOut = false,
                ProductAssetSuffix = "_SI_05",
                GroupAssetSuffix = "_PIMG_05"
            },
            new()
            {
                SlotNumber = 6,
                Kind = ImageSlotKind.Secondary,
                DisplayLabel = "Secondary Image 6",
                ReferenceType = "Secondary_Image_06",
                SupportsCutOut = false,
                ProductAssetSuffix = "_SI_06",
                GroupAssetSuffix = "_PIMG_06"
            },
            new()
            {
                SlotNumber = 7,
                Kind = ImageSlotKind.Secondary,
                DisplayLabel = "Secondary Image 7",
                ReferenceType = "Secondary_Image_07",
                SupportsCutOut = false,
                ProductAssetSuffix = "_SI_07",
                GroupAssetSuffix = "_PIMG_07"
            },
            new()
            {
                SlotNumber = 8,
                Kind = ImageSlotKind.Secondary,
                DisplayLabel = "Secondary Image 8",
                ReferenceType = "Secondary_Image_08",
                SupportsCutOut = false,
                ProductAssetSuffix = "_SI_08",
                GroupAssetSuffix = "_PIMG_08"
            },
            new()
            {
                SlotNumber = 9,
                Kind = ImageSlotKind.Secondary,
                DisplayLabel = "Secondary Image 9",
                ReferenceType = "Secondary_Image_09",
                SupportsCutOut = false,
                ProductAssetSuffix = "_SI_09",
                GroupAssetSuffix = "_PIMG_09"
            }
        };
}