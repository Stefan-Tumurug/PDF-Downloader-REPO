using PdfDownloader.Core.Abstractions;
using PdfDownloader.Core.Application;
using System.Text;

namespace PdfDownloader.Core.Infrastructure;

/// <summary>
/// CSV implementation of <see cref="IStatusWriter"/>.
/// 
/// Responsibility:
/// - Persist a collection of <see cref="DownloadStatusRow"/> entries
///   to a CSV file inside a configured root folder.
/// 
/// Design notes:
/// - The Core layer defines the row model.
/// - This class handles formatting and file system interaction.
/// - CSV escaping follows standard RFC-style quoting rules:
///     - Values containing comma, quote or newline are wrapped in quotes.
///     - Quotes inside values are doubled (" → "").
/// </summary>
public sealed class CsvStatusWriter(string rootFolder) : IStatusWriter
{
    private readonly string _rootFolder = rootFolder;

    /// <summary>
    /// Writes the provided status rows to disk as UTF-8 CSV.
    /// 
    /// Ensures the target directory exists before writing.
    /// Overwrites the file if it already exists.
    /// </summary>
    public async Task WriteAsync(
        string relativePath,
        IReadOnlyList<DownloadStatusRow> rows,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.Combine(_rootFolder, relativePath);

        // Ensure directory exists
        string? dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Build CSV in-memory before writing once to disk.
        // This avoids partial writes and keeps IO simple.
        StringBuilder sb = new();
        sb.AppendLine("BRnum,AttemptedUrl,Status,Error");

        foreach (DownloadStatusRow row in rows)
        {
            sb.Append(Escape(row.BrNum)).Append(',')
              .Append(Escape(row.AttemptedUrl)).Append(',')
              .Append(row.Status).Append(',')
              .Append(Escape(row.Error)).AppendLine();
        }

        await File.WriteAllTextAsync(
            fullPath,
            sb.ToString(),
            Encoding.UTF8,
            cancellationToken);
    }

    /// <summary>
    /// Escapes a value according to basic CSV quoting rules.
    /// 
    /// - If no special characters are present, value is returned as-is.
    /// - If value contains comma, quote or newline:
    ///     - Wrap in double quotes
    ///     - Escape quotes by doubling them
    /// </summary>
    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        bool mustQuote =
            value.Contains(',') ||
            value.Contains('"') ||
            value.Contains('\n') ||
            value.Contains('\r');

        if (!mustQuote)
        {
            return value;
        }

        string escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}