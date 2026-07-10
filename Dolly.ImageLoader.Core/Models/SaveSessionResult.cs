namespace Dolly.ImageLoader.Core.Models;

public sealed class SaveSessionResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public string? WorkingFolderPath { get; init; }
    public string? ZipFilePath { get; init; }
}