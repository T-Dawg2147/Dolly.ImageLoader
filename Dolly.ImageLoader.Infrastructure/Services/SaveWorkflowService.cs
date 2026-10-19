using Dolly.ImageLoader.Core.Enums;
using Dolly.ImageLoader.Core.Interfaces;
using Dolly.ImageLoader.Core.Models;
using Microsoft.Extensions.Logging;

namespace Dolly.ImageLoader.Infrastructure.Services;

public sealed class SaveWorkflowService : ISaveWorkflowService
{
    private readonly IImageImportMetadataRepository _imageImportMetadataRepository;
    private readonly IAssetNamingService _assetNamingService;
    private readonly IImageFileService _imageFileService;
    private readonly IImageProcessingService _imageProcessingService;
    private readonly IAssetLinkService _assetLinkService;
    private readonly IMetadataService _metadataService;
    private readonly IPackageService _packageService;
    private readonly IAmazonImageService _amazonImageService;
    private readonly ILogger<SaveWorkflowService> _logger;

    public SaveWorkflowService(
        IImageImportMetadataRepository imageImportMetadataRepository,
        IAssetNamingService assetNamingService,
        IImageFileService imageFileService,
        IImageProcessingService imageProcessingService,
        IAssetLinkService assetLinkService,
        IMetadataService metadataService,
        IPackageService packageService,
        IAmazonImageService amazonImageService,
        ILogger<SaveWorkflowService> logger)
    {
        _imageImportMetadataRepository = imageImportMetadataRepository;
        _assetNamingService = assetNamingService;
        _imageFileService = imageFileService;
        _imageProcessingService = imageProcessingService;
        _assetLinkService = assetLinkService;
        _metadataService = metadataService;
        _packageService = packageService;
        _amazonImageService = amazonImageService;
        _logger = logger;
    }

    public async Task<SaveSessionResult> ExecuteAsync(SaveRequest request, CancellationToken cancellationToken)
    {
        var workingFolder = await _imageFileService.CreateWorkingFolderAsync(cancellationToken);
        var metadataRecords = new List<MetadataRecord>();
        var stagedFiles = new List<string>();

        try
        {
            var changedSlots = request.Slots
                .Where(x => x.PendingAction != PendingActionType.None)
                .ToList();

            if (changedSlots.Count == 0)
            {
                return new SaveSessionResult
                {
                    Success = true,
                    Message = "No changes to save.",
                    WorkingFolderPath = workingFolder
                };
            }

            var targetId = request.ProductContext.ProductId;
            
            /*var targetId = request.TargetMode == TargetMode.Product
                ? request.ProductContext.ProductId
                : request.ProductContext.ParentGroupId;*/

            var importMetadata = await _imageImportMetadataRepository.GetByTargetIdAsync(targetId, cancellationToken);
            if (importMetadata is null)
            {
                return new SaveSessionResult
                {
                    Success = false,
                    Message = $"Could not retrieve image import metadata for target '{targetId}'.",
                    WorkingFolderPath = workingFolder
                };
            }

            if (string.IsNullOrWhiteSpace(importMetadata.ImageName))
            {
                return new SaveSessionResult
                {
                    Success = false,
                    Message = $"fn_ImagePath returned an empty ImageName for target '{targetId}'.",
                    WorkingFolderPath = workingFolder
                };
            }

            if (string.IsNullOrWhiteSpace(importMetadata.HierarchyClassification))
            {
                return new SaveSessionResult
                {
                    Success = false,
                    Message = $"fn_ImagePath returned an empty Hierarchy_Classification for target '{targetId}'.",
                    WorkingFolderPath = workingFolder
                };
            }

            if (string.IsNullOrWhiteSpace(importMetadata.SectionIdentifier))
            {
                return new SaveSessionResult
                {
                    Success = false,
                    Message = $"fn_ImagePath returned an empty Section_Identifier for target '{targetId}'.",
                    WorkingFolderPath = workingFolder
                };
            }

            foreach (var slot in changedSlots)
            {
                var plan = _assetNamingService.BuildPlan(
                    request.ProductContext,
                    request.TargetMode,
                    slot.Definition,
                    importMetadata);

                if (slot.PendingAction == PendingActionType.Clear)
                {
                    var imageNameToUnlink = !string.IsNullOrWhiteSpace(slot.CurrentAssetName)
                        ? slot.CurrentAssetName
                        : plan.PrimaryAssetName;

                    await _assetLinkService.UnlinkAsync(
                        plan.TargetId,
                        imageNameToUnlink,
                        slot.Definition.ReferenceType,
                        request.Username,
                        cancellationToken);

                    if (slot.Definition.SupportsCutOut)
                    {
                        var cutOutToUnlink = slot.CurrentAssetName;
                        if (!string.IsNullOrWhiteSpace(cutOutToUnlink) &&
                            cutOutToUnlink.EndsWith("_CO", StringComparison.OrdinalIgnoreCase))
                        {
                            await _assetLinkService.UnlinkAsync(
                                plan.TargetId,
                                cutOutToUnlink,
                                "Primary_Cut_Out",
                                request.Username,
                                cancellationToken);
                        }
                        else if (!string.IsNullOrWhiteSpace(plan.CutOutAssetName))
                        {
                            await _assetLinkService.UnlinkAsync(
                                plan.TargetId,
                                plan.CutOutAssetName,
                                "Primary_Cut_Out",
                                request.Username,
                                cancellationToken);
                        }
                    }

                    continue;
                }

                if (string.IsNullOrWhiteSpace(slot.PendingSourceFilePath))
                {
                    continue;
                }

                var validation = await _imageProcessingService.ValidateImageAsync(slot.PendingSourceFilePath, cancellationToken);
                if (!validation.IsValid)
                {
                    return new SaveSessionResult
                    {
                        Success = false,
                        Message = $"Image validation failed for {slot.Definition.DisplayLabel}: {validation.Message}",
                        WorkingFolderPath = workingFolder
                    };
                }

                foreach (var outputFileName in plan.OutputFileNames)
                {
                    var stagedPath = await _imageFileService.CopyIntoWorkingFolderAsync(
                        slot.PendingSourceFilePath,
                        workingFolder,
                        outputFileName,
                        cancellationToken);

                    stagedFiles.Add(stagedPath);
                }

                await _assetLinkService.LinkAsync(
                    plan.TargetId,
                    plan.PrimaryAssetName,
                    slot.Definition.ReferenceType,
                    request.Username,
                    cancellationToken);

                metadataRecords.Add(new MetadataRecord
                {
                    FileName = $"{plan.PrimaryAssetName}.jpg",
                    Product = plan.TargetId,
                    Reference = slot.Definition.ReferenceType,
                    HierarchyClassification = plan.HierarchyClassification
                });

                if (slot.Definition.SupportsCutOut && !string.IsNullOrWhiteSpace(plan.CutOutAssetName))
                {
                    await _assetLinkService.LinkAsync(
                        plan.TargetId,
                        plan.CutOutAssetName,
                        "Primary_Cut_Out",
                        request.Username,
                        cancellationToken);

                    metadataRecords.Add(new MetadataRecord
                    {
                        FileName = $"{plan.CutOutAssetName}.jpg",
                        Product = plan.TargetId,
                        Reference = "Primary_Cut_Out",
                        HierarchyClassification = plan.HierarchyClassification
                    });
                }

                var amazonPlan = _amazonImageService.BuildPlan(
                    request.ProductContext,
                    request.TargetMode,
                    slot.Definition);

                await _amazonImageService.WriteImagesAsync(
                    slot.PendingSourceFilePath,
                    amazonPlan,
                    cancellationToken);
            }

            var metadataCsvPath = await _metadataService.ExportMetadataCsvAsync(metadataRecords, workingFolder, cancellationToken);
            stagedFiles.Add(metadataCsvPath);

            var zipFileName = $"{importMetadata.SectionIdentifier}{DateTime.Now:ddMMyyyyHHmmss}.zip";
            var zipPath = await _packageService.CreateZipAsync(workingFolder, zipFileName, stagedFiles, cancellationToken);
            await _packageService.CopyToHotfolderAsync(zipPath, cancellationToken);

            return new SaveSessionResult
            {
                Success = true,
                Message = "Changes saved successfully.",
                WorkingFolderPath = workingFolder,
                ZipFilePath = zipPath
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Save workflow failed.");

            return new SaveSessionResult
            {
                Success = false,
                Message = $"Save workflow failed: {ex.Message}",
                WorkingFolderPath = workingFolder
            };
        }
    }
}