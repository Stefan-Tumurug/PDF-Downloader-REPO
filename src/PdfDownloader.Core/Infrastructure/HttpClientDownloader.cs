using PdfDownloader.Core.Abstractions;
using System.Net.Http;

namespace PdfDownloader.Core.Infrastructure;

/// <summary>
/// Default implementation of <see cref="IHttpDownloader"/> using <see cref="HttpClient"/>.
/// 
/// Responsibility:
/// - Perform HTTP GET requests and return raw response bytes.
/// 
/// Design notes:
/// - HttpClient is injected to allow proper lifetime management
///   (single shared instance per application).
/// - Timeout, retry and validation logic live in the Core layer
///   (DownloadRunner), not here.
/// - This class is intentionally thin to keep infrastructure simple.
/// </summary>
public sealed class HttpClientDownloader : IHttpDownloader
{
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Creates a new HTTP downloader.
    /// </summary>
    /// <param name="httpClient">
    /// Pre-configured HttpClient instance.
    /// The caller is responsible for its lifetime.
    /// </param>
    public HttpClientDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Downloads the content at the specified URL as a byte array.
    /// </summary>
    public async Task<byte[]> GetBytesAsync(Uri url, CancellationToken cancellationToken)
    {
        return await _httpClient.GetByteArrayAsync(url, cancellationToken);
    }
}