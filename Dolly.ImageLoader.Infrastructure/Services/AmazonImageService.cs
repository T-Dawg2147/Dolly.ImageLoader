using System.IO;
using Dolly.ImageLoader.Core.Enums;
using Dolly.ImageLoader.Core.Interfaces;
using Dolly.ImageLoader.Core.Models;
using Dolly.ImageLoader.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Dolly.ImageLoader.Infrastructure.Services;

public sealed class AmazonImageService : IAmazonImageService
{
    private readonly PathOptions _pathOptions;
    private readonly ILogger<AmazonImageService> _logger;

    public AmazonImageService(
        IOptions<PathOptions> pathOptions,
        ILogger<AmazonImageService> logger)
    {
        _pathOptions = pathOptions.Value;
        _logger = logger;
    }

    public AmazonImagePlan BuildPlan(
        ProductContext productContext,
        TargetMode targetMode,
        ImageSlotDefinition slotDefinition)
    {
        var amazonName = GetAmazonName(productContext, targetMode);

        if (slotDefinition.Kind == ImageSlotKind.Primary)
        {
            return new AmazonImagePlan
            {
                ImportFileName = $"{amazonName}.jpg",
                LargeFileName = $"{amazonName}.jpg",
                MediumFileName = $"{amazonName}.jpg",
                SmallFileName = $"{amazonName}.jpg",
                IsSecondary = false
            };
        }

        var slotSuffix = slotDefinition.SlotNumber.ToString();
        var fileName = $"{amazonName}_{slotSuffix}.jpg";

        return new AmazonImagePlan
        {
            ImportFileName = fileName,
            LargeFileName = fileName,
            MediumFileName = fileName,
            SmallFileName = fileName,
            IsSecondary = true
        };
    }

    public async Task WriteImagesAsync(
        string sourceFilePath,
        AmazonImagePlan plan,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("Source image file not found.", sourceFilePath);
        }

        var importRoot = Environment.ExpandEnvironmentVariables(_pathOptions.AmazonImportRoot);

        var largeRoot = Environment.ExpandEnvironmentVariables(
            plan.IsSecondary ? _pathOptions.AmazonSecondaryLargeRoot : _pathOptions.AmazonPrimaryLargeRoot);

        var mediumRoot = Environment.ExpandEnvironmentVariables(
            plan.IsSecondary ? _pathOptions.AmazonSecondaryMediumRoot : _pathOptions.AmazonPrimaryMediumRoot);

        var smallRoot = Environment.ExpandEnvironmentVariables(
            plan.IsSecondary ? _pathOptions.AmazonSecondarySmallRoot : _pathOptions.AmazonPrimarySmallRoot);

        var importPath = Path.Combine(importRoot, plan.ImportFileName);
        var largePath = Path.Combine(largeRoot, plan.LargeFileName);
        var mediumPath = Path.Combine(mediumRoot, plan.MediumFileName);
        var smallPath = Path.Combine(smallRoot, plan.SmallFileName);

        Directory.CreateDirectory(importRoot);
        Directory.CreateDirectory(largeRoot);
        Directory.CreateDirectory(mediumRoot);
        Directory.CreateDirectory(smallRoot);

        await SaveResizedJpegAsync(sourceFilePath, importPath, 700, 700, cancellationToken);
        await SaveResizedJpegAsync(sourceFilePath, largePath, 500, 500, cancellationToken);
        await SaveResizedJpegAsync(sourceFilePath, mediumPath, 300, 300, cancellationToken);
        await SaveResizedJpegAsync(sourceFilePath, smallPath, 128, 128, cancellationToken);

        _logger.LogInformation(
            "Amazon image outputs written. Import={ImportPath}, Large={LargePath}, Medium={MediumPath}, Small={SmallPath}",
            importPath, largePath, mediumPath, smallPath);
    }

    private static string GetAmazonName(ProductContext productContext, TargetMode targetMode)
    {
        var rawName = targetMode == TargetMode.Product
            ? productContext.ProductName
            : productContext.BaseCode;

        return (rawName ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static async Task SaveResizedJpegAsync(
        string sourceFilePath,
        string destinationPath,
        int maxWidth,
        int maxHeight,
        CancellationToken cancellationToken)
    {
        using var image = await Image.LoadAsync(sourceFilePath, cancellationToken);

        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Mode = ResizeMode.Max,
            Size = new Size(maxWidth, maxHeight)
        }));

        await image.SaveAsJpegAsync(
            destinationPath,
            new JpegEncoder
            {
                Quality = 90
            },
            cancellationToken);
    }
}