namespace PdfDownloader.Core.Abstractions;

/// <summary>
/// Downloads bytes from a URL.
/// </summary>
public interface IHttpDownloader
{
    Task<byte[]> GetBytesAsync(Uri url, CancellationToken cancellationToken);
}