using PdfDownloader.Core.Abstractions;
using PdfDownloader.Core.Application;
using System.Text;

namespace PdfDownloader.Core.Infrastructure;

/// <summary>
/// Writes status rows to a CSV file.
/// </summary>
public sealed class CsvStatusWriter(string rootFolder) : IStatusWriter
{
    private readonly string _rootFolder = rootFolder;

    public async Task WriteAsync(
        string relativePath,
        IReadOnlyList<DownloadStatusRow> rows,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.Combine(_rootFolder, relativePath);

        string? dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        StringBuilder sb = new();
        sb.AppendLine("BRnum,AttemptedUrl,Status,Error");

        foreach (DownloadStatusRow row in rows)
        {
            sb.Append(Escape(row.BrNum)).Append(',')
              .Append(Escape(row.AttemptedUrl)).Append(',')
              .Append(row.Status).Append(',')
              .Append(Escape(row.Error)).AppendLine();
        }

        await File.WriteAllTextAsync(fullPath, sb.ToString(), Encoding.UTF8, cancellationToken);
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        bool mustQuote = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        if (!mustQuote)
        {
            return value;
        }

        string escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }
}