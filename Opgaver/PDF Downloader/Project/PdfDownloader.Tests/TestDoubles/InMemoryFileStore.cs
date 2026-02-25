using PdfDownloader.Core.Abstractions;

namespace PdfDownloader.Tests.TestDoubles;

/// <summary>
/// In-memory file store to avoid disk IO in unit tests.
/// </summary>
public sealed class InMemoryFileStore : IFileStore
{
    private readonly Dictionary<string, byte[]> _files = [];

    public bool Exists(string relativePath)
    {
        return _files.ContainsKey(relativePath);
    }

    public Task SaveAsync(string relativePath, byte[] bytes, CancellationToken cancellationToken)
    {
        _files[relativePath] = bytes;
        return Task.CompletedTask;
    }

    public byte[]? TryGet(string relativePath)
    {
        return _files.TryGetValue(relativePath, out byte[]? bytes) ? bytes : null;
    }

    public void Seed(string relativePath, byte[] bytes)
    {
        _files[relativePath] = bytes;
    }

    public int Count => _files.Count;
}