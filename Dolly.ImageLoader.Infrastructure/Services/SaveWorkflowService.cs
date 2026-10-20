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

    public Task<SaveSessionResult> ExecuteAsync(SaveRequest request, CancellationToken cancellationToken)
        => ExecuteBatchAsync([request], cancellationToken);

    public async Task<SaveSessionResult> ExecuteBatchAsync(IReadOnlyList<SaveRequest> requests, CancellationToken ct)
    {
        var workingFolder = await _imageFileService.CreateWorkingFolderAsync(ct);
        var metadataRecords = new List<MetadataRecord>();
        var stagedFiles = new List<string>();

        try
        {
            var work = requests
                .Select(r => (Request: r,
                    ChangedSlots: r.Slots.Where(x => x.PendingAction != PendingActionType.None).ToList()))
                .Where(x => x.ChangedSlots.Count > 0)
                .ToList();

            if (work.Count == 0)
            {
                return new SaveSessionResult
                {
                    Success = true,
                    Message = "No changes to save.",
                    WorkingFolderPath = workingFolder
                };
            }
            
            var importMetadataByTarget = new Dictionary<string, ImageImportMetadata>();
            foreach (var (request, changedSlots) in work)
            {
                var targetId = request.ProductContext.ProductId;

                var importMetaData = await _imageImportMetadataRepository.GetByTargetIdAsync(targetId, ct);

                // metadata is null
                if (importMetaData is null)
                    return Fail($"Could not retrieve image import metadata for target '{targetId}'.");
                // empty image name
                if (string.IsNullOrWhiteSpace(importMetaData.ImageName))
                    return Fail($"fn_ImagePath returned an empty ImageName for target '{{targetId}}'.");
                // empty classification
                if (string.IsNullOrWhiteSpace(importMetaData.HierarchyClassification))
                    return Fail(
                        $"fn_ImagePath returned an empty Hierarchy Classification for target '{{targetId}}'.");
                // empty section identifier
                if (string.IsNullOrWhiteSpace(importMetaData.SectionIdentifier))
                    return Fail($"fn_ImagePath returned an empty Section Identifier for target '{{targetId}}'.");

                importMetadataByTarget[targetId] = importMetaData;

                foreach (var slot in changedSlots.Where(s => s.PendingAction != PendingActionType.Clear 
                                                             && !string.IsNullOrWhiteSpace(s.PendingSourceFilePath)))
                {
                    var validation = await _imageProcessingService.ValidateImageAsync(
                        slot.PendingSourceFilePath!, ct);
                    if (!validation.IsValid)
                        return Fail(
                            $"Image validation failed for {targetId} / {slot.Definition.DisplayLabel}: {validation.Message}");
                }
            }

            foreach (var (request, changedSlots) in work)
            {
                var targetId = request.ProductContext.ProductId;
                var importMetadata = importMetadataByTarget[targetId];

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
                        ct);

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
                                ct);
                        }
                        else if (!string.IsNullOrWhiteSpace(plan.CutOutAssetName))
                        {
                            await _assetLinkService.UnlinkAsync(
                                plan.TargetId,
                                plan.CutOutAssetName,
                                "Primary_Cut_Out",
                                request.Username,
                                ct);
                        }
                    }

                    continue;
                }

                if (string.IsNullOrWhiteSpace(slot.PendingSourceFilePath))
                {
                    continue;
                }

                var validation = await _imageProcessingService.ValidateImageAsync(slot.PendingSourceFilePath, ct);
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
                        ct);

                    stagedFiles.Add(stagedPath);
                }

                await _assetLinkService.LinkAsync(
                    plan.TargetId,
                    plan.PrimaryAssetName,
                    slot.Definition.ReferenceType,
                    request.Username,
                    ct);

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
                        ct);

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
                    ct);
                }
            }

            var metadataCsvPath = await _metadataService.ExportMetadataCsvAsync(metadataRecords, workingFolder, ct);
            stagedFiles.Add(metadataCsvPath);

            var sectionIdentifier = importMetadataByTarget.Values.First().SectionIdentifier;
            var zipFileName = $"{sectionIdentifier}{DateTime.Now:ddMMyyyyHHmmss}.zip";
            var zipPath = await _packageService.CreateZipAsync(workingFolder, zipFileName, stagedFiles, ct);
            await _packageService.CopyToHotfolderAsync(zipPath, ct);

            return new SaveSessionResult
            {
                Success = true,
                Message = $"Saved {work.Count} target(s) in one package.",
                WorkingFolderPath = workingFolder,
                ZipFilePath = zipPath
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch save workflow failed.");
            return Fail($"Save workflow failed: {ex.Message}");
        }

        SaveSessionResult Fail(string msg) => new()
        {
            Success = false,
            Message = msg,
            WorkingFolderPath = workingFolder
        };
    }
}