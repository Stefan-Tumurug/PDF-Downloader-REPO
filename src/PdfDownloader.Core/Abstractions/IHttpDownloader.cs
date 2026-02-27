namespace PdfDownloader.Core.Abstractions;

/// <summary>
/// Abstraction over an HTTP download mechanism.
/// 
/// Responsibility:
/// - Retrieve raw byte content from a given URI.
/// 
/// The implementation decides how HTTP communication is performed
/// (HttpClient, retry policies, timeout handling, etc.).
/// 
/// This abstraction keeps the Core layer independent of
/// concrete networking infrastructure and enables deterministic tests.
/// </summary>
public interface IHttpDownloader
{
    /// <summary>
    /// Downloads the resource at the specified URI as raw bytes.
    /// </summary>
    /// <param name="url">
    /// Absolute URI of the resource to download.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to cancel the request.
    /// </param>
    /// <returns>
    /// The downloaded content as a byte array.
    /// </returns>
    Task<byte[]> GetBytesAsync(Uri url, CancellationToken cancellationToken);
}