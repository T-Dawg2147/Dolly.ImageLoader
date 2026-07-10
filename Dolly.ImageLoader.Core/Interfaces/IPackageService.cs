namespace Dolly.ImageLoader.Core.Interfaces;

public interface IPackageService
{
    Task<string> CreateZipAsync(
        string workingFolderPath,
        string zipFileName,
        IReadOnlyList<string> filePaths,
        CancellationToken ct);

    Task CopyToHotfolderAsync(
        string zipFilePath,
        CancellationToken ct);
}