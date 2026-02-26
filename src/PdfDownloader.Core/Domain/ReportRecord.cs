namespace PdfDownloader.Core.Domain;

/// <summary>
/// Represents a single report entry from the dataset.
/// Contains only metadata required for downloading and naming the PDF.
/// </summary>
public sealed class ReportRecord
{
    /// <summary>
    /// Creates a new immutable report record.
    /// </summary>
    /// <param name="brNum">Business register number used as PDF file name.</param>
    /// <param name="primaryUrl">Primary PDF URL from the dataset.</param>
    /// <param name="fallbackUrl">Fallback URL used if primary download fails.</param>
    public ReportRecord(string brNum, Uri? primaryUrl, Uri? fallbackUrl)
    {
        BrNum = brNum;
        PrimaryUrl = primaryUrl;
        FallbackUrl = fallbackUrl;
    }

    /// <summary>
    /// Business register number.
    /// Used to name the downloaded PDF file.
    /// </summary>
    public string BrNum { get; }

    /// <summary>
    /// Primary URL pointing directly to the PDF file (if available).
    /// </summary>
    public Uri? PrimaryUrl { get; }

    /// <summary>
    /// Alternative URL used when the primary URL fails.
    /// May point to a HTML page or secondary PDF.
    /// </summary>
    public Uri? FallbackUrl { get; }
}