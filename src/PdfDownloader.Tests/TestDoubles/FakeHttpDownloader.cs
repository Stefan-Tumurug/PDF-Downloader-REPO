using PdfDownloader.Core.Abstractions;
using System.Net;

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
    private readonly Dictionary<string, int> _throwTimesLeftByUrl = [];
    private readonly Dictionary<string, HttpStatusCode> _throwStatusByUrl = [];

    public IReadOnlyList<string> RequestedUrls => _requestedUrls;

    public void SetupBytes(Uri url, byte[] bytes)
    {
        _responsesByUrl[url.ToString()] = bytes;
    }

    public void SetupThrow(Uri url)
    {
        _throwOnUrl.Add(url.ToString());
    }
    public void SetupThrowTimes(Uri url, int times)
    {
        if (times < 1) throw new ArgumentOutOfRangeException(nameof(times));
        _throwTimesLeftByUrl[url.ToString()] = times;
    }


public void SetupThrowWithStatus(Uri url, HttpStatusCode statusCode)
{
    _throwStatusByUrl[url.ToString()] = statusCode;
}

public void SetupThrowTimesWithStatus(Uri url, int times, HttpStatusCode statusCode)
{
    if (times < 1) throw new ArgumentOutOfRangeException(nameof(times));
    _throwTimesLeftByUrl[url.ToString()] = times;
    _throwStatusByUrl[url.ToString()] = statusCode;
}
    public Task<byte[]> GetBytesAsync(Uri url, CancellationToken cancellationToken)
    {
        _requestedUrls.Add(url.ToString());

        if (_throwTimesLeftByUrl.TryGetValue(url.ToString(), out int timesLeft) && timesLeft > 0)
        {
            _throwTimesLeftByUrl[url.ToString()] = timesLeft - 1;

            if (_throwStatusByUrl.TryGetValue(url.ToString(), out HttpStatusCode statusCode))
            {
                throw new HttpRequestException("Simulated HTTP failure.", inner: null, statusCode: statusCode);
            }

            throw new HttpRequestException("Simulated HTTP failure.");
        }

        if (_throwOnUrl.Contains(url.ToString()))
        {
            if (_throwStatusByUrl.TryGetValue(url.ToString(), out HttpStatusCode statusCode))
            {
                throw new HttpRequestException("Simulated HTTP failure.", inner: null, statusCode: statusCode);
            }

            throw new HttpRequestException("Simulated HTTP failure.");
        }

        if (_responsesByUrl.TryGetValue(url.ToString(), out byte[]? bytes))
        {
            return Task.FromResult(bytes);
        }

        throw new InvalidOperationException($"No fake response configured for URL: {url}");
    }
}