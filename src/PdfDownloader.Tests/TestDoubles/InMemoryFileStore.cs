using PdfDownloader.Core.Abstractions;

namespace PdfDownloader.Tests.TestDoubles;

/// <summary>
/// In-memory implementation of <see cref="IFileStore"/> for unit tests.
///
/// Responsibility:
/// - Simulate file existence checks
/// - Capture saved file bytes without touching disk
///
/// This fake:
/// - Stores files in a Dictionary keyed by relative path
/// - Allows seeding pre-existing files
/// - Exposes stored content for assertions
/// </summary>
public sealed class InMemoryFileStore : IFileStore
{
    private readonly Dictionary<string, byte[]> _files = [];

    /// <summary>
    /// Returns true if a file has been stored under the given path.
    /// </summary>
    public bool Exists(string relativePath)
    {
        return _files.ContainsKey(relativePath);
    }

    /// <summary>
    /// Stores or replaces the file at the given path.
    /// </summary>
    public Task SaveAsync(string relativePath, byte[] bytes, CancellationToken cancellationToken)
    {
        _files[relativePath] = bytes;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns stored bytes if present; otherwise null.
    /// </summary>
    public byte[]? TryGet(string relativePath)
    {
        return _files.TryGetValue(relativePath, out byte[]? bytes)
            ? bytes
            : null;
    }

    /// <summary>
    /// Preloads a file into the store.
    /// Useful for testing "file already exists" scenarios.
    /// </summary>
    public void Seed(string relativePath, byte[] bytes)
    {
        _files[relativePath] = bytes;
    }

    /// <summary>
    /// Gets the number of stored files.
    /// Useful for asserting stop conditions.
    /// </summary>
    public int Count => _files.Count;
}