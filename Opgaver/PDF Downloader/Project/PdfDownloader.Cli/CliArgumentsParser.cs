using System;
using System.IO;

namespace PdfDownloader.Cli;

public sealed class CliArgumentsParser
{
    private readonly string _baseDirectory;

    public CliArgumentsParser(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
    }

    public bool TryParse(
        string[] args,
        out CliArguments? parsed,
        out string? errorMessage,
        out int exitCode)
    {
        parsed = null;
        errorMessage = null;
        exitCode = 0;

        if (!TryResolvePaths(args, out string xlsxPath, out string outputFolder, out errorMessage, out exitCode))
        {
            return false;
        }

        if (!File.Exists(xlsxPath))
        {
            errorMessage = $"Excel file not found: {xlsxPath}";
            exitCode = 2;
            return false;
        }

        if (!TryEnsureDirectory(outputFolder, out errorMessage))
        {
            exitCode = 3;
            return false;
        }

        parsed = new CliArguments(xlsxPath, outputFolder);
        return true;
    }

    private bool TryResolvePaths(
        string[] args,
        out string xlsxPath,
        out string outputFolder,
        out string? errorMessage,
        out int exitCode)
    {
        xlsxPath = string.Empty;
        outputFolder = string.Empty;
        errorMessage = null;
        exitCode = 0;

        if (args.Length == 0)
        {
            string repoRoot = FindRepoRoot(_baseDirectory);
            xlsxPath = Path.Combine(repoRoot, "data", "GRI_2017_2020.xlsx");
            outputFolder = Path.Combine(repoRoot, "out");
            return true;
        }

        if (args.Length == 2)
        {
            xlsxPath = args[0];
            outputFolder = args[1];
            return true;
        }

        errorMessage =
            "Usage:\n" +
            "  PdfDownloader.Cli <path-to-xlsx> <output-folder>\n" +
            "Or run without arguments to use defaults:\n" +
            "  data/GRI_2017_2020.xlsx and out/";
        exitCode = 2;
        return false;
    }

    private static bool TryEnsureDirectory(string folder, out string? errorMessage)
    {
        errorMessage = null;

        try
        {
            Directory.CreateDirectory(folder);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Could not create output folder: {folder}\n{ex.Message}";
            return false;
        }
    }

    private static string FindRepoRoot(string startDirectory)
    {
        DirectoryInfo? dir = new DirectoryInfo(startDirectory);

        while (dir is not null)
        {
            string dataFolder = Path.Combine(dir.FullName, "data");
            if (Directory.Exists(dataFolder))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}