using ClosedXML.Excel;
using PdfDownloader.Core.Abstractions;
using PdfDownloader.Core.Domain;

namespace PdfDownloader.Core.Infrastructure;

/// <summary>
/// Reads report metadata from an Excel file and maps rows to <see cref="ReportRecord"/> objects.
/// This class is responsible only for Excel parsing, not downloading or file storage.
/// </summary>
public sealed class ExcelReportSource : IReportSource
{
    private readonly string _xlsxPath;
    private readonly string _worksheetName;

    /// <summary>
    /// Creates a new ExcelReportSource.
    /// </summary>
    /// <param name="xlsxPath">Path to the Excel dataset.</param>
    /// <param name="worksheetName">Optional worksheet name. Falls back to first sheet if not found.</param>
    public ExcelReportSource(string xlsxPath, string worksheetName = "GRI_2017_2020")
    {
        _xlsxPath = xlsxPath;
        _worksheetName = worksheetName;
    }

    /// <summary>
    /// Reads all valid report records from the Excel file.
    /// Rows without BR number or URLs are ignored.
    /// </summary>
    public IReadOnlyList<ReportRecord> ReadAll()
    {
        if (!File.Exists(_xlsxPath))
        {
            throw new FileNotFoundException("Excel file not found.", _xlsxPath);
        }

        using XLWorkbook workbook = new XLWorkbook(_xlsxPath);
        IXLWorksheet sheet = GetWorksheet(workbook);

        // Build a lookup of column name -> column index from the header row.
        Dictionary<string, int> columns = ReadHeaderColumns(sheet);

        List<ReportRecord> records = new List<ReportRecord>();

        // Assume first used row is header, data starts on the next row.
        IEnumerable<IXLRow> dataRows = sheet.RowsUsed().Skip(1);

        foreach (IXLRow row in dataRows)
        {
            ReportRecord? record = TryMapRow(row, columns);
            if (record is null)
            {
                // Skip rows that do not contain usable data.
                continue;
            }

            records.Add(record);
        }

        return records;
    }

    /// <summary>
    /// Returns the requested worksheet or falls back to the first worksheet.
    /// </summary>
    private IXLWorksheet GetWorksheet(XLWorkbook workbook)
    {
        if (workbook.TryGetWorksheet(_worksheetName, out IXLWorksheet? sheet) && sheet is not null)
        {
            return sheet;
        }

        // Fallback: use first worksheet if named one is missing.
        return workbook.Worksheets.First();
    }

    /// <summary>
    /// Reads the header row and maps column names to column numbers.
    /// Uses case-insensitive keys for robustness.
    /// </summary>
    private static Dictionary<string, int> ReadHeaderColumns(IXLWorksheet sheet)
    {
        IXLRow? headerRow = sheet.FirstRowUsed();
        if (headerRow is null)
        {
            throw new InvalidDataException("Worksheet does not contain a header row.");
        }

        Dictionary<string, int> columns =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (IXLCell cell in headerRow.CellsUsed())
        {
            string header = cell.GetString().Trim();
            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            columns[header] = cell.Address.ColumnNumber;
        }

        return columns;
    }

    /// <summary>
    /// Attempts to map a single Excel row into a ReportRecord.
    /// Returns null if required fields are missing.
    /// </summary>
    private static ReportRecord? TryMapRow(IXLRow row, Dictionary<string, int> columns)
    {
        string brNum = ReadString(row, columns, "BRnum", "BRNummer");
        if (string.IsNullOrWhiteSpace(brNum))
        {
            // BR number is mandatory for file naming.
            return null;
        }

        Uri? primaryUrl = TryReadUri(row, columns, "Pdf_URL");
        Uri? fallbackUrl = TryReadUri(row, columns, "Report Html Address");

        // Skip rows with no usable URLs.
        if (primaryUrl is null && fallbackUrl is null)
        {
            return null;
        }

        return new ReportRecord(brNum, primaryUrl, fallbackUrl);
    }

    /// <summary>
    /// Reads a string from the first matching column name.
    /// Allows multiple possible headers for robustness.
    /// </summary>
    private static string ReadString(
        IXLRow row,
        Dictionary<string, int> columns,
        params string[] possibleHeaders)
    {
        foreach (string header in possibleHeaders)
        {
            if (!columns.TryGetValue(header, out int col))
            {
                continue;
            }

            string value = row.Cell(col).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Attempts to read and parse an absolute URI from a column.
    /// Returns null if missing or invalid.
    /// </summary>
    private static Uri? TryReadUri(IXLRow row, Dictionary<string, int> columns, string header)
    {
        if (!columns.TryGetValue(header, out int col))
        {
            return null;
        }

        string raw = row.Cell(col).GetString().Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (Uri.TryCreate(raw, UriKind.Absolute, out Uri? uri))
        {
            return uri;
        }

        return null;
    }
}