# PDF Downloader

## Overview

PDF Downloader is a .NET console application that reads report metadata from an Excel dataset and downloads valid PDF reports to a specified output folder.

The project is structured using a layered architecture (Core / CLI / Tests) to separate orchestration logic from infrastructure and presentation.
The solution is designed to be testable, maintainable, and easy to extend.

This project is a prototype and intentionally limits each run to **10 successful PDF downloads**.

## Features

- Reads report metadata from `GRI_2017_2020.xlsx`
- Maps rows to domain objects (`ReportRecord`)
- Downloads PDFs using primary URL with fallback support
- Validates files by checking `%PDF-` header (prevents HTML being saved as PDF)
- Saves files as `<BRnum>.pdf`
- Stops after **10 successful downloads**
- Writes `status.csv` with:
  - BR number
  - Attempted URL
  - Status (Downloaded / Failed / SkippedExists)
  - Error message
- Continues execution even if individual downloads fail
- Prints summary counts to console

## Project Structure

PdfDownloader.Core - Domain, orchestration and abstractions  
PdfDownloader.Cli - Console entry point (composition root)  
PdfDownloader.Tests - Unit tests (planned / in progress)

## How to Run

### Option 1: Run from Visual Studio (no arguments)

Press **Run** in Visual Studio.

If no arguments are provided, the program automatically uses:

- `data/GRI_2017_2020.xlsx`
- `out/`

These paths are resolved relative to the repository root.

### Option 2: Run from terminal (explicit arguments)

From the `Project` folder:

`dotnet run --project ".\PdfDownloader.Cli\" -- "<path-to-xlsx>" "<output-folder>"`

Example:

`dotnet run --project ".\PdfDownloader.Cli\" -- "C:\data\GRI_2017_2020.xlsx" "C:\out"`

## Output

The output folder will contain:

- Up to 10 PDF files named `<BRnum>.pdf`
- `status.csv` describing the result of each attempted download

Console output includes:

- Total records loaded
- Downloaded / Failed / Skipped counts
- Output folder path
- Status file path

## Architecture

The application follows SOLID principles:

- `DownloadRunner` coordinates the workflow
- Infrastructure concerns are abstracted via interfaces:
  - `IHttpDownloader`
  - `IFileStore`
  - `IStatusWriter`
- CLI acts only as composition root
- Core contains no direct HTTP or file system logic

This makes the system testable and extensible.

## Documentation

Additional documentation (UML diagrams, use cases, fremgangsmåde, tidsregistrering) is located in:

`Opgaver/PDF Downloader/Documentation (Danish)`

## Technology

- .NET
- C#
- ClosedXML (Excel parsing)

## Notes

This implementation is intentionally limited to 10 successful downloads per run to avoid excessive network usage during development.

The original reference implementation was written in Python.
C# was chosen to better demonstrate object-oriented design, layering, and testability.

## Next Steps

- Add unit tests for `DownloadRunner`
- Expand documentation
- Remove download limit for production use