using System.IO;
using Dolly.ImageLoader.Core.Interfaces;
using Dolly.ImageLoader.Core.Models;
using Dolly.ImageLoader.Core.Options;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;

namespace Dolly.ImageLoader.Infrastructure.Services;

public sealed class ImageSharpImageProcessingService : IImageProcessingService
{
    private readonly ImageOptions _imageOptions;

    public ImageSharpImageProcessingService(IOptions<ImageOptions> imageOptions)
    {
        _imageOptions = imageOptions.Value;
    }

    public async Task<ImageValidationResult> ValidateImageAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return Invalid("No file path was provided.");
            }

            if (!File.Exists(filePath))
            {
                return Invalid($"File does not exist: {filePath}");
            }

            var extension = Path.GetExtension(filePath);
            if (_imageOptions.AllowedExtensions.Length > 0 &&
                !_imageOptions.AllowedExtensions.Any(x => string.Equals(x, extension, StringComparison.OrdinalIgnoreCase)))
            {
                return Invalid($"Invalid file type '{extension}'. Allowed types: {string.Join(", ", _imageOptions.AllowedExtensions)}");
            }

            var fileInfo = new FileInfo(filePath);
            var fileSizeBytes = fileInfo.Length;

            if (fileSizeBytes > _imageOptions.MaxFileSizeBytes)
            {
                return Invalid(
                    $"File size exceeds the maximum allowed size of {FormatBytes(_imageOptions.MaxFileSizeBytes)}. " +
                    $"Actual size: {FormatBytes(fileSizeBytes)}.");
            }

            using var image = await Image.LoadAsync(filePath, cancellationToken);

            var width = image.Width;
            var height = image.Height;

            var horizontalDpi = image.Metadata.HorizontalResolution;
            var verticalDpi = image.Metadata.VerticalResolution;

            if (width < _imageOptions.MinWidth || height < _imageOptions.MinHeight)
            {
                return Invalid(
                    $"Image resolution is too small. Minimum allowed resolution is {_imageOptions.MinWidth}x{_imageOptions.MinHeight}. " +
                    $"Actual resolution: {width}x{height}.",
                    width,
                    height,
                    horizontalDpi,
                    verticalDpi,
                    fileSizeBytes);
            }

            if (width > _imageOptions.MaxWidth || height > _imageOptions.MaxHeight)
            {
                return Invalid(
                    $"Image resolution is too large. Maximum allowed resolution is {_imageOptions.MaxWidth}x{_imageOptions.MaxHeight}. " +
                    $"Actual resolution: {width}x{height}.",
                    width,
                    height,
                    horizontalDpi,
                    verticalDpi,
                    fileSizeBytes);
            }

            var result = new ImageValidationResult
            {
                IsValid = true,
                Message = "Image is valid.",
                Width = width,
                Height = height,
                HorizontalDpi = horizontalDpi,
                VerticalDpi = verticalDpi,
                FileSizeBytes = fileSizeBytes
            };

            if (horizontalDpi <= 0 || verticalDpi <= 0)
            {
                result.HasWarning = true;
                result.WarningMessage = "Image DPI metadata is missing or invalid. Expected minimum DPI is 72, but the image can still be used.";
            }
            else if (horizontalDpi < _imageOptions.MinDpi || verticalDpi < _imageOptions.MinDpi)
            {
                result.HasWarning = true;
                result.WarningMessage =
                    $"Image DPI appears lower than the recommended minimum of {_imageOptions.MinDpi}. " +
                    $"Actual DPI: {horizontalDpi:0.##}x{verticalDpi:0.##}. The image can still be used.";
            }

            return result;
        }
        catch (Exception ex)
        {
            return Invalid($"Image validation failed: {ex.Message}");
        }
    }

    private static ImageValidationResult Invalid(
        string message,
        int width = 0,
        int height = 0,
        double horizontalDpi = 0,
        double verticalDpi = 0,
        long fileSizeBytes = 0)
    {
        return new ImageValidationResult
        {
            IsValid = false,
            Message = message,
            Width = width,
            Height = height,
            HorizontalDpi = horizontalDpi,
            VerticalDpi = verticalDpi,
            FileSizeBytes = fileSizeBytes
        };
    }

    private static string FormatBytes(long bytes)
    {
        const double kb = 1024d;
        const double mb = kb * 1024d;

        if (bytes >= mb)
        {
            return $"{bytes / mb:0.##} MB";
        }

        if (bytes >= kb)
        {
            return $"{bytes / kb:0.##} KB";
        }

        return $"{bytes} bytes";
    }
}