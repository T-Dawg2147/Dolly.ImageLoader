namespace Dolly.ImageLoader.Core.Interfaces;

public interface IImageFileService
{
    Task<bool> FileExistsAsync(string path, CancellationToken ct);
    Task<string> CreateWorkingFolderAsync(CancellationToken ct);
    Task<string> CopyIntoWorkingFolderAsync(string sourceFilePath, string workingFolderPath, string destinationFilePath, CancellationToken ct);
    Task<string> CopyFileAsync(string sourceFilePath, string destinationFilePath, CancellationToken ct);
}