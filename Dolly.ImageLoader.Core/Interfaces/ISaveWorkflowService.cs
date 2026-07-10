using Dolly.ImageLoader.Core.Models;

namespace Dolly.ImageLoader.Core.Interfaces;

public interface ISaveWorkflowService
{
    Task<SaveSessionResult> ExecuteAsync(SaveRequest request, CancellationToken ct);
}