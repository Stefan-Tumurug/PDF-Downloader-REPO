namespace PdfDownloader.Cli;

public sealed class CliArguments
{
    public CliArguments(string xlsxPath, string outputFolder)
    {
        XlsxPath = xlsxPath;
        OutputFolder = outputFolder;
    }

    public string XlsxPath { get; }
    public string OutputFolder { get; }
}