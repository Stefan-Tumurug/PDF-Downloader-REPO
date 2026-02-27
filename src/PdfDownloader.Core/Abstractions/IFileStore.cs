namespace PdfDownloader.Core.Abstractions;

/// <summary>
/// Abstraction over a file storage mechanism.
/// 
/// Responsibility:
/// - Check whether a file already exists
/// - Persist binary content to a storage location
/// 
/// The implementation decides where and how files are stored
/// (local disk, NAS, cloud storage, etc.).
/// 
/// Using an abstraction keeps the Core layer independent of
/// concrete file system concerns and improves testability.
/// </summary>
public interface IFileStore
{
    /// <summary>
    /// Determines whether a file exists at the given relative path.
    /// </summary>
    /// <param name="relativePath">
    /// Path relative to the configured storage root.
    /// </param>
    /// <returns>
    /// True if the file exists; otherwise false.
    /// </returns>
    bool Exists(string relativePath);

    /// <summary>
    /// Saves binary content to storage.
    /// </summary>
    /// <param name="relativePath">
    /// Path relative to the configured storage root.
    /// </param>
    /// <param name="bytes">
    /// File content to persist.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the operation.
    /// </param>
    Task SaveAsync(string relativePath, byte[] bytes, CancellationToken cancellationToken);
}