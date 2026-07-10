using System.IO;
using Dolly.ImageLoader.Core.Interfaces;
using Dolly.ImageLoader.Core.Options;
using Microsoft.Extensions.Options;

namespace Dolly.ImageLoader.Infrastructure.Services;

public sealed class LocalImageFileService : IImageFileService
{
    private readonly PathOptions _pathOptions;

    public LocalImageFileService(IOptions<PathOptions> pathOptions)
    {
        _pathOptions = pathOptions.Value;
    }

    public Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken)
    {
        return Task.FromResult(File.Exists(path));
    }

    public Task<string> CreateWorkingFolderAsync(CancellationToken cancellationToken)
    {
        var root = Environment.ExpandEnvironmentVariables(_pathOptions.WorkingRoot);
        var folder = Path.Combine(root, $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}");

        Directory.CreateDirectory(folder);

        return Task.FromResult(folder);
    }

    public Task<string> CopyIntoWorkingFolderAsync(
        string sourceFilePath,
        string workingFolderPath,
        string destinationFileName,
        CancellationToken ct)
    {
        Directory.CreateDirectory(workingFolderPath);

        var destinationPath = Path.Combine(workingFolderPath, destinationFileName);
        File.Copy(sourceFilePath, destinationPath, overwrite: true);

        return Task.FromResult(destinationPath);
    }

    public Task<string> CopyFileAsync(string sourceFilePath, string destinationFilePath, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.Copy(sourceFilePath, destinationFilePath, overwrite: true);
        return Task.FromResult(destinationFilePath);
    }
}