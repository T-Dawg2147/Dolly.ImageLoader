using System.IO;
using Dolly.ImageLoader.Core.Interfaces;
using Dolly.ImageLoader.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dolly.ImageLoader.Infrastructure.Services;

public sealed class WorkingDirectoryCleanupService : IWorkingDirectoryCleanupService
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(1);

    private readonly PathOptions _pathOptions;
    private readonly ILogger<WorkingDirectoryCleanupService> _logger;

    public WorkingDirectoryCleanupService(
        IOptions<PathOptions> pathOptions,
        ILogger<WorkingDirectoryCleanupService> logger)
    {
        _pathOptions = pathOptions.Value;
        _logger = logger;
    }

    public Task CleanupOldWorkingDirectoriesAsync(CancellationToken ct = default)
    {
        try
        {
            var workingRoot = Environment.ExpandEnvironmentVariables(_pathOptions.WorkingRoot);

            if (string.IsNullOrWhiteSpace(workingRoot) || !Directory.Exists(workingRoot))
            {
                _logger.LogInformation("Working root does not exist, skipping cleanup. Path: {WorkingRoot}", workingRoot);
                return Task.CompletedTask;
            }

            var cutoffUtc = DateTime.UtcNow.Subtract(MaxAge);

            foreach (var directoryPath in Directory.EnumerateDirectories(workingRoot))
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var directoryInfo = new DirectoryInfo(directoryPath);

                    var lastWriteUtc = directoryInfo.LastWriteTimeUtc;
                    if (lastWriteUtc >= cutoffUtc)
                        continue;
                    
                    _logger.LogInformation(
                        "Deleting old working directory {DirectoryPath}. Last write UTC: {LastWriteUtc}",
                        directoryPath,
                        lastWriteUtc);

                    Directory.Delete(directoryPath, recursive: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete working directory {DirectoryPath}", directoryPath);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Working directory cleanup failed.");
        }

        return Task.CompletedTask;
    }
}