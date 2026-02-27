namespace PdfDownloader.Cli;

/// <summary>
/// Immutable value object representing validated CLI input.
/// 
/// Responsibility:
/// Holds the Excel path and output folder required to start the application.
/// 
/// This class contains no parsing or validation logic.
/// It is intended to be created by a dedicated argument parser
/// and passed into the composition root.
/// </summary>
public sealed class CliArguments
{
    /// <summary>
    /// Creates a new set of CLI arguments.
    /// </summary>
    /// <param name="xlsxPath">
    /// Path to the Excel file containing report metadata.
    /// </param>
    /// <param name="outputFolder">
    /// Folder where downloaded PDFs and status output will be written.
    /// </param>
    public CliArguments(string xlsxPath, string outputFolder)
    {
        XlsxPath = xlsxPath;
        OutputFolder = outputFolder;
    }

    /// <summary>
    /// Gets the Excel (.xlsx) file path.
    /// </summary>
    public string XlsxPath { get; }

    /// <summary>
    /// Gets the output directory path.
    /// </summary>
    public string OutputFolder { get; }
}