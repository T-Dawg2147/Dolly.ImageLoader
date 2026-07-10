namespace Dolly.ImageLoader.Core.Interfaces;

public interface IWorkingDirectoryCleanupService
{
    Task CleanupOldWorkingDirectoriesAsync(CancellationToken ct = default);
}