using PdfDownloader.Core.Abstractions;

namespace PdfDownloader.Core.Infrastructure;

/// <summary>
/// File system implementation of <see cref="IFileStore"/>.
/// 
/// Responsibility:
/// - Persist files under a configured root folder
/// - Resolve relative paths against that root
/// 
/// Design notes:
/// - The Core layer never interacts directly with System.IO.
/// - All path resolution is centralized here.
/// - The root folder can represent:
///     - Local disk
///     - Network share (NAS)
///     - Mounted drive
/// </summary>
public sealed class LocalFileStore : IFileStore
{
    private readonly string _rootFolder;

    /// <summary>
    /// Creates a new file store rooted at the specified folder.
    /// </summary>
    /// <param name="rootFolder">
    /// Base directory where all files will be stored.
    /// </param>
    public LocalFileStore(string rootFolder)
    {
        _rootFolder = rootFolder;
    }

    /// <summary>
    /// Determines whether a file exists at the given relative path.
    /// </summary>
    public bool Exists(string relativePath)
    {
        string fullPath = Path.Combine(_rootFolder, relativePath);
        return File.Exists(fullPath);
    }

    /// <summary>
    /// Saves a file to disk.
    /// 
    /// Ensures that the target directory exists before writing.
    /// Overwrites the file if it already exists.
    /// </summary>
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