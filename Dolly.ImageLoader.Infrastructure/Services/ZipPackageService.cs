using System.IO;
using System.IO.Compression;
using Dolly.ImageLoader.Core.Interfaces;
using Dolly.ImageLoader.Core.Options;
using Microsoft.Extensions.Options;

namespace Dolly.ImageLoader.Infrastructure.Services;

public sealed class ZipPackageService : IPackageService
{
    private readonly PathOptions _pathOptions;

    public ZipPackageService(IOptions<PathOptions> pathOptions)
    {
        _pathOptions = pathOptions.Value;
    }

    public Task<string> CreateZipAsync(
        string workingFolderPath,
        string zipFileName,
        IReadOnlyList<string> filePaths,
        CancellationToken cancellationToken)
    {
        var zipPath = Path.Combine(workingFolderPath, zipFileName);

        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);

        foreach (var filePath in filePaths.Where(File.Exists))
        {
            archive.CreateEntryFromFile(filePath, Path.GetFileName(filePath));
        }

        return Task.FromResult(zipPath);
    }

    public Task CopyToHotfolderAsync(string zipFilePath, CancellationToken cancellationToken)
    {
        var destination = Path.Combine(_pathOptions.HotfolderRoot, Path.GetFileName(zipFilePath));
        File.Copy(zipFilePath, destination, overwrite: true);

        return Task.CompletedTask;
    }
}