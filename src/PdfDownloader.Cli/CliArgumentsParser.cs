using System;
using System.IO;

namespace PdfDownloader.Cli;

/// <summary>
/// Responsible for parsing and validating CLI input.
/// 
/// Responsibilities:
/// - Resolves Excel and output paths from raw arguments
/// - Applies default paths when no arguments are provided
/// - Validates that the Excel file exists
/// - Ensures the output directory can be created
/// 
/// This class does NOT start the application.
/// It only translates raw CLI input into a valid <see cref="CliArguments"/> instance.
/// </summary>
public sealed class CliArgumentsParser
{
    private readonly string _baseDirectory;

    /// <summary>
    /// Creates a new parser.
    /// </summary>
    /// <param name="baseDirectory">
    /// Base directory used to locate the repository root when resolving default paths.
    /// Typically AppContext.BaseDirectory.
    /// </param>
    public CliArgumentsParser(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
    }

    /// <summary>
    /// Attempts to parse and validate CLI arguments.
    /// </summary>
    /// <param name="args">Raw command-line arguments.</param>
    /// <param name="parsed">Resulting validated arguments if successful.</param>
    /// <param name="errorMessage">Error message if parsing fails.</param>
    /// <param name="exitCode">Exit code to return from the application.</param>
    /// <returns>True if parsing and validation succeed; otherwise false.</returns>
    public bool TryParse(
        string[] args,
        out CliArguments? parsed,
        out string? errorMessage,
        out int exitCode)
    {
        parsed = null;
        errorMessage = null;
        exitCode = 0;

        // Resolve raw paths (either defaults or user-provided)
        if (!TryResolvePaths(args, out string xlsxPath, out string outputFolder, out errorMessage, out exitCode))
        {
            return false;
        }

        // Validate that the Excel file exists
        if (!File.Exists(xlsxPath))
        {
            errorMessage = $"Excel file not found: {xlsxPath}";
            exitCode = 2;
            return false;
        }

        // Ensure output directory is available or creatable
        if (!TryEnsureDirectory(outputFolder, out errorMessage))
        {
            exitCode = 3;
            return false;
        }

        parsed = new CliArguments(xlsxPath, outputFolder);
        return true;
    }

    /// <summary>
    /// Resolves input paths based on CLI arguments.
    /// 
    /// - No arguments → use default repo-based paths
    /// - Two arguments → treat as explicit xlsx + output folder
    /// - Any other count → invalid usage
    /// </summary>
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

    /// <summary>
    /// Ensures that the output directory exists.
    /// Creates it if necessary.
    /// </summary>
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

    /// <summary>
    /// Walks up the directory tree until a folder containing a "data" directory is found.
    /// Used to locate the repository root when running without arguments.
    /// </summary>
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

        // Fallback to current working directory if repo root is not found
        return Directory.GetCurrentDirectory();
    }
}