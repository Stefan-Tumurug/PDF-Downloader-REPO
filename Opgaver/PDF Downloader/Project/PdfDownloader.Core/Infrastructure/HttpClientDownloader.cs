using PdfDownloader.Core.Abstractions;
using System.Net.Http;

namespace PdfDownloader.Core.Infrastructure;

/// <summary>
/// Default HTTP downloader using HttpClient.
/// </summary>
public sealed class HttpClientDownloader : IHttpDownloader
{
    private readonly HttpClient _httpClient;

    public HttpClientDownloader(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<byte[]> GetBytesAsync(Uri url, CancellationToken cancellationToken)
    {
        return await _httpClient.GetByteArrayAsync(url, cancellationToken);
    }
}