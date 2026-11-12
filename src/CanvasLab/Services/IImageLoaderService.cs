using CanvasLab.Models;

namespace CanvasLab.Services;

public interface IImageLoaderService
{
    Task<ImageDocument> LoadAsync(string filePath, CancellationToken cancellationToken = default);
}

