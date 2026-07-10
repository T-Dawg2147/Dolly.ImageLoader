using System.IO;
using Dolly.ImageLoader.Core.Interfaces;
using Dolly.ImageLoader.Core.Models;
using Dolly.ImageLoader.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Dolly.ImageLoader.Infrastructure.Services;

public sealed class ImageNormalizationService : IImageNormalizationService
{
    private static readonly string[] PreferredJpegExtensions = [".jpg", ".jpeg"];

    private readonly PathOptions _pathOptions;
    private readonly ImageOptions _imageOptions;
    private readonly ILogger<ImageNormalizationService> _logger;

    public ImageNormalizationService(
        IOptions<PathOptions> pathOptions,
        IOptions<ImageOptions> imageOptions,
        ILogger<ImageNormalizationService> logger)
    {
        _pathOptions = pathOptions.Value;
        _imageOptions = imageOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ImageNormalizationResult>> InspectAsync(
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken)
    {
        var results = new List<ImageNormalizationResult>();

        foreach (var filePath in filePaths
                     .Where(File.Exists)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var needsNormalization = await NeedsNormalizationAsync(filePath, cancellationToken);

                results.Add(new ImageNormalizationResult
                {
                    OriginalFilePath = filePath,
                    OutputFilePath = filePath,
                    NeedsNormalization = needsNormalization,
                    WasNormalized = false,
                    IsSuccess = true,
                    Message = needsNormalization
                        ? "Normalization recommended."
                        : "No normalization needed."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to inspect image {FilePath} for normalization.", filePath);

                results.Add(new ImageNormalizationResult
                {
                    OriginalFilePath = filePath,
                    OutputFilePath = filePath,
                    NeedsNormalization = false,
                    WasNormalized = false,
                    IsSuccess = false,
                    Message = $"Inspection failed: {ex.Message}"
                });
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<ImageNormalizationResult>> NormalizeIfNeededAsync(
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken)
    {
        var results = new List<ImageNormalizationResult>();

        foreach (var filePath in filePaths
                     .Where(File.Exists)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var needsNormalization = await NeedsNormalizationAsync(filePath, cancellationToken);
                if (!needsNormalization)
                {
                    results.Add(new ImageNormalizationResult
                    {
                        OriginalFilePath = filePath,
                        OutputFilePath = filePath,
                        NeedsNormalization = false,
                        WasNormalized = false,
                        IsSuccess = true,
                        Message = "No normalization needed."
                    });

                    continue;
                }

                var normalizedPath = await NormalizeToTemporaryJpegAsync(filePath, cancellationToken);

                results.Add(new ImageNormalizationResult
                {
                    OriginalFilePath = filePath,
                    OutputFilePath = normalizedPath,
                    NeedsNormalization = true,
                    WasNormalized = true,
                    IsSuccess = true,
                    Message = "Image normalized successfully."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to normalize image {FilePath}.", filePath);

                results.Add(new ImageNormalizationResult
                {
                    OriginalFilePath = filePath,
                    OutputFilePath = filePath,
                    NeedsNormalization = true,
                    WasNormalized = false,
                    IsSuccess = false,
                    Message = $"Normalization failed: {ex.Message}"
                });
            }
        }

        return results;
    }

    private async Task<bool> NeedsNormalizationAsync(string filePath, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(filePath);

        using var image = await Image.LoadAsync(filePath, cancellationToken);

        var isJpeg = PreferredJpegExtensions.Any(x =>
            string.Equals(x, extension, StringComparison.OrdinalIgnoreCase));

        var horizontalDpi = image.Metadata.HorizontalResolution;
        var verticalDpi = image.Metadata.VerticalResolution;

        var dpiMissingOrLow =
            horizontalDpi <= 0 ||
            verticalDpi <= 0 ||
            horizontalDpi < _imageOptions.MinDpi ||
            verticalDpi < _imageOptions.MinDpi;

        var resolutionOutsideBounds =
            image.Width < _imageOptions.MinWidth ||
            image.Height < _imageOptions.MinHeight ||
            image.Width > _imageOptions.MaxWidth ||
            image.Height > _imageOptions.MaxHeight;

        return !isJpeg || dpiMissingOrLow || resolutionOutsideBounds;
    }

    private async Task<string> NormalizeToTemporaryJpegAsync(string originalFilePath, CancellationToken cancellationToken)
    {
        var normalizedRoot = Path.Combine(
            Environment.ExpandEnvironmentVariables(_pathOptions.WorkingRoot),
            "NormalizedPool");

        Directory.CreateDirectory(normalizedRoot);

        var outputFileName =
            $"{Path.GetFileNameWithoutExtension(originalFilePath)}_{Guid.NewGuid():N}.jpg";

        var outputPath = Path.Combine(normalizedRoot, outputFileName);

        using var source = await Image.LoadAsync<Rgba32>(originalFilePath, cancellationToken);

        using var normalized = new Image<Rgba32>(source.Width, source.Height, Color.White);
        normalized.Mutate(ctx => ctx.DrawImage(source, 1f));

        ResizeIfTooLarge(normalized);

        normalized.Metadata.HorizontalResolution = 72;
        normalized.Metadata.VerticalResolution = 72;
        normalized.Metadata.ResolutionUnits = PixelResolutionUnit.PixelsPerInch;

        normalized.Metadata.ExifProfile = null;
        normalized.Metadata.XmpProfile = null;

        var encoder = new JpegEncoder
        {
            Quality = 95
        };

        await normalized.SaveAsJpegAsync(outputPath, encoder, cancellationToken);

        return outputPath;
    }

    private void ResizeIfTooLarge(Image<Rgba32> image)
    {
        var maxDimension = Math.Max(image.Width, image.Height);
        var maxAllowedDimension = Math.Max(_imageOptions.MaxWidth, _imageOptions.MaxHeight);

        if (maxDimension <= maxAllowedDimension)
        {
            return;
        }

        if (image.Width >= image.Height)
        {
            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(maxAllowedDimension, 0),
                Mode = ResizeMode.Max
            }));
        }
        else
        {
            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(0, maxAllowedDimension),
                Mode = ResizeMode.Max
            }));
        }
    }
}