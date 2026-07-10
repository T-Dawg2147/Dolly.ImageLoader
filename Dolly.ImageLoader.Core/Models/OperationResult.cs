using System.Diagnostics;

namespace Dolly.ImageLoader.Core.Models;

public sealed class OperationResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;

    public static OperationResult Ok(string message = "")
        => new() { Success = true, Message = message };

    public static OperationResult Fail(string message)
        => new() { Success = false, Message = message };
}