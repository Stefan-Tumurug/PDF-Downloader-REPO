using PdfDownloader.Core.Abstractions;
using System.Net;

namespace PdfDownloader.Tests.TestDoubles;

/// <summary>
/// Deterministic fake implementation of <see cref="IHttpDownloader"/> for unit tests.
///
/// Responsibilities:
/// - Return pre-configured byte responses per URL
/// - Simulate HTTP failures (optionally with HTTP status code)
/// - Track all requested URLs in call order for assertions
///
/// Usage patterns:
/// - SetupBytes(url, bytes)         => successful response
/// - SetupThrow(url)               => always throws on that URL
/// - SetupThrowTimes(url, n)       => throws n times, then behaves normally
/// - SetupThrowTimesWithStatus(...)=> throws n times with a status code
///
/// Notes:
/// - This fake does not perform real IO.
/// - CancellationToken is accepted to match the interface but not used here,
///   because tests configure behavior deterministically.
/// </summary>
public sealed class FakeHttpDownloader : IHttpDownloader
{
    private readonly Dictionary<string, byte[]> _responsesByUrl = [];
    private readonly HashSet<string> _throwOnUrl = [];
    private readonly List<string> _requestedUrls = [];
    private readonly Dictionary<string, int> _throwTimesLeftByUrl = [];
    private readonly Dictionary<string, HttpStatusCode> _throwStatusByUrl = [];

    /// <summary>
    /// Gets URLs requested by the system under test, in the order they were called.
    /// Useful for asserting primary-then-fallback behavior and retry counts.
    /// </summary>
    public IReadOnlyList<string> RequestedUrls => _requestedUrls;

    /// <summary>
    /// Configures a successful byte response for the given URL.
    /// </summary>
    public void SetupBytes(Uri url, byte[] bytes)
    {
        _responsesByUrl[url.ToString()] = bytes;
    }

    /// <summary>
    /// Configures the given URL to always throw an <see cref="HttpRequestException"/>.
    /// </summary>
    public void SetupThrow(Uri url)
    {
        _throwOnUrl.Add(url.ToString());
    }

    /// <summary>
    /// Configures the given URL to throw an <see cref="HttpRequestException"/> a fixed number of times.
    /// After the counter reaches zero, the fake will return bytes if configured via <see cref="SetupBytes"/>.
    /// </summary>
    public void SetupThrowTimes(Uri url, int times)
    {
        if (times < 1) throw new ArgumentOutOfRangeException(nameof(times));
        _throwTimesLeftByUrl[url.ToString()] = times;
    }

    /// <summary>
    /// Configures an HTTP status code to be included when throwing for the given URL.
    /// This is used to test transient vs deterministic behavior in the runner.
    /// </summary>
    public void SetupThrowWithStatus(Uri url, HttpStatusCode statusCode)
    {
        _throwStatusByUrl[url.ToString()] = statusCode;
    }

    /// <summary>
    /// Configures the given URL to throw a fixed number of times and include a status code.
    /// </summary>
    public void SetupThrowTimesWithStatus(Uri url, int times, HttpStatusCode statusCode)
    {
        if (times < 1) throw new ArgumentOutOfRangeException(nameof(times));
        _throwTimesLeftByUrl[url.ToString()] = times;
        _throwStatusByUrl[url.ToString()] = statusCode;
    }

    /// <summary>
    /// Returns configured bytes or throws based on configured failure rules.
    /// Records every requested URL for verification in tests.
    /// </summary>
    public Task<byte[]> GetBytesAsync(Uri url, CancellationToken cancellationToken)
    {
        string key = url.ToString();
        _requestedUrls.Add(key);

        // "Throw N times" has highest priority to simulate retries deterministically.
        if (_throwTimesLeftByUrl.TryGetValue(key, out int timesLeft) && timesLeft > 0)
        {
            _throwTimesLeftByUrl[key] = timesLeft - 1;
            throw CreateHttpFailure(key);
        }

        // Permanent failure for specific URLs.
        if (_throwOnUrl.Contains(key))
        {
            throw CreateHttpFailure(key);
        }

        // Successful response.
        if (_responsesByUrl.TryGetValue(key, out byte[]? bytes))
        {
            return Task.FromResult(bytes);
        }

        // Misconfigured test setup (better than silently returning empty bytes).
        throw new InvalidOperationException($"No fake response configured for URL: {url}");
    }

    private HttpRequestException CreateHttpFailure(string urlKey)
    {
        if (_throwStatusByUrl.TryGetValue(urlKey, out HttpStatusCode statusCode))
        {
            return new HttpRequestException("Simulated HTTP failure.", inner: null, statusCode: statusCode);
        }

        return new HttpRequestException("Simulated HTTP failure.");
    }
}