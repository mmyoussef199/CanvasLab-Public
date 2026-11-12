using CanvasLab.Models;

namespace CanvasLab.Services;

public interface IImageOptimizationService
{
    Task<ImageOptimizationResult> OptimizeAsync(ImageOptimizationRequest request, CancellationToken cancellationToken = default);
    Task SaveAsync(ImageOptimizationResult result, string filePath, bool overwriteApproved,
        CancellationToken cancellationToken = default);
}

