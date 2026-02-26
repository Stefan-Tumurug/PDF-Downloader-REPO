namespace PdfDownloader.Core.Abstractions;

/// <summary>
/// Persists files to a storage location (local folder, NAS path, etc.).
/// </summary>
public interface IFileStore
{
    bool Exists(string relativePath);
    Task SaveAsync(string relativePath, byte[] bytes, CancellationToken cancellationToken);
}