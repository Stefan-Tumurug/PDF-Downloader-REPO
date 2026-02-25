using PdfDownloader.Core.Abstractions;

namespace PdfDownloader.Core.Infrastructure;

/// <summary>
/// Stores files under a single root folder (local path or NAS share).
/// </summary>
public sealed class LocalFileStore : IFileStore
{
    private readonly string _rootFolder;

    public LocalFileStore(string rootFolder)
    {
        _rootFolder = rootFolder;
    }

    public bool Exists(string relativePath)
    {
        string fullPath = Path.Combine(_rootFolder, relativePath);
        return File.Exists(fullPath);
    }

    public async Task SaveAsync(string relativePath, byte[] bytes, CancellationToken cancellationToken)
    {
        string fullPath = Path.Combine(_rootFolder, relativePath);

        string? dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);
    }
}