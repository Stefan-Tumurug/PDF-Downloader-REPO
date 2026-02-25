using PdfDownloader.Core.Abstractions;

namespace PdfDownloader.Tests.TestDoubles;

/// <summary>
/// Deterministic fake HTTP downloader.
/// Configure responses per URL.
/// </summary>
public sealed class FakeHttpDownloader : IHttpDownloader
{
    private readonly Dictionary<string, byte[]> _responsesByUrl = [];
    private readonly HashSet<string> _throwOnUrl = [];
    private readonly List<string> _requestedUrls = [];

    public IReadOnlyList<string> RequestedUrls => _requestedUrls;

    public void SetupBytes(Uri url, byte[] bytes)
    {
        _responsesByUrl[url.ToString()] = bytes;
    }

    public void SetupThrow(Uri url)
    {
        _throwOnUrl.Add(url.ToString());
    }

    public Task<byte[]> GetBytesAsync(Uri url, CancellationToken cancellationToken)
    {
        _requestedUrls.Add(url.ToString());

        if (_throwOnUrl.Contains(url.ToString()))
        {
            throw new HttpRequestException("Simulated HTTP failure.");
        }

        if (_responsesByUrl.TryGetValue(url.ToString(), out byte[]? bytes))
        {
            return Task.FromResult(bytes);
        }

        throw new InvalidOperationException($"No fake response configured for URL: {url}");
    }
}