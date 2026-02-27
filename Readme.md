# PDF Downloader

![CI](https://github.com/Stefan-Tumurug/PDF-Downloader-REPO/actions/workflows/ci.yml/badge.svg)

## Overview

PDF Downloader is a layered .NET application that reads report metadata
from an Excel dataset and downloads valid PDF reports to a specified
output folder.

The system is built around strict separation of concerns and SOLID
principles. Both the CLI and GUI reuse the same orchestration logic from
the Core layer.

Each run is intentionally limited to a user-defined number of successful
downloads to prevent excessive network usage.

------------------------------------------------------------------------

## Architecture

The system follows a layered architecture:

-   Presentation layer (CLI / GUI)
-   Core orchestration (`DownloadRunner`)
-   Domain models
-   Infrastructure implementations

The Core layer depends only on abstractions:

-   `IHttpDownloader`
-   `IFileStore`
-   `IStatusWriter`
-   `IReportSource`

Infrastructure implementations provide the concrete behavior:

-   `HttpClientDownloader`
-   `LocalFileStore`
-   `CsvStatusWriter`
-   `ExcelReportSource`

Progress reporting is implemented through `IProgress<DownloadProgress>`,
keeping the Core layer UI-agnostic.

------------------------------------------------------------------------

## UML Documentation

Detailed design diagrams are available in the `docs/` folder:

-   `architecture.png` --- High-level layered architecture diagram\
-   `domain-class-diagram.png` --- Detailed class and dependency
    relationships

The architecture diagram illustrates:

-   Clear separation between Presentation, Core, and Infrastructure\
-   Dependency inversion via interfaces\
-   How `DownloadRunner` coordinates the workflow

The domain class diagram shows:

-   Core orchestration flow\
-   Interface contracts\
-   Concrete infrastructure implementations\
-   Relationships between `DownloadRunner`, `DownloadOptions`,
    `DownloadStatusRow`, and domain models

------------------------------------------------------------------------

## Execution Flow

For each record:

1.  Validate BR number\
2.  Resolve output filename (`<BRnum>.pdf`)\
3.  Skip if file exists and overwrite is disabled\
4.  Attempt Primary URL
    -   Retry transient HTTP failures\
    -   Validate `%PDF-` header before saving\
5.  If Primary fails and fallback exists:
    -   Attempt Fallback URL\
6.  Record final `DownloadStatus`\
7.  Stop when `MaxSuccessfulDownloads` is reached\
8.  Write `status.csv` once at the end of execution

------------------------------------------------------------------------

## Features

-   Reads report metadata from Excel (`.xlsx`)
-   Maps rows to domain objects (`ReportRecord`)
-   Downloads PDFs using:
    -   Primary URL
    -   Fallback URL (if provided)
-   Validates files using `%PDF-` header check
-   Supports retry for transient HTTP failures
-   Writes `status.csv` with:
    -   BR number
    -   Attempted URL
    -   Status (Downloaded / Failed / SkippedExists)
    -   Error message
-   Continues execution even if individual downloads fail
-   Supports cancellation (Ctrl+C in CLI / Cancel in GUI)
-   Optional overwrite of existing output files
-   Stops after successful download limit is reached
-   Progress reporting in GUI via `DownloadProgress`

------------------------------------------------------------------------

## Output

The output folder will contain:

PDF files named:

    <BRnum>.pdf

And a generated:

    status.csv

Each status row describes:

-   BR number
-   Attempted URL
-   Result status
-   Error message (if applicable)

------------------------------------------------------------------------

## Testing

The project includes deterministic unit tests using MSTest.

External dependencies such as HTTP and file system access are replaced
with test doubles to ensure predictable behavior.

### Covered Scenarios

The following core behaviors are verified:

-   Successful PDF download using Primary URL
-   Fallback URL is used when Primary fails
-   Fallback is not used when Primary succeeds
-   Files are skipped when they already exist
    (`OverwriteExisting = false`)
-   Existing files are replaced when overwrite is enabled
-   Execution stops after the configured maximum number of successful
    downloads
-   Non-PDF responses are rejected
-   Unsupported URL schemes are handled gracefully
-   HTTP failures correctly fall back without retrying deterministic
    errors
-   Status results are generated consistently

Tests run in parallel at method level and contain no shared mutable
state.

Run tests:

``` bash
dotnet test
```

------------------------------------------------------------------------

## How to Run

### CLI

Run without arguments:

``` bash
dotnet run --project ".\PdfDownloader.Cli\"
```

Run with explicit arguments:

``` bash
dotnet run --project ".\PdfDownloader.Cli\" -- "<path-to-xlsx>" "<output-folder>"
```

Press Ctrl+C to cancel execution.

### GUI

Set `PdfDownloader.Gui` as startup project in Visual Studio.

The GUI allows:

-   Browsing for Excel file
-   Selecting output folder
-   Choosing whether to overwrite existing files
-   Viewing per-record progress
-   Cancelling downloads

------------------------------------------------------------------------

## CI

GitHub Actions automatically runs:

-   Restore
-   Build
-   Unit tests

On every push and pull request.

------------------------------------------------------------------------

## Known Limitations

Some external links may fail due to:

-   Dead hosts
-   Redirects
-   Forbidden access (403)
-   HTML pages disguised as PDFs

These cases are expected and recorded in `status.csv`.

------------------------------------------------------------------------

## Technology

-   .NET
-   C#
-   WPF
-   ClosedXML
-   MSTest

------------------------------------------------------------------------

## Future Improvements

-   Parallel downloads
-   Resume support
-   Improved redirect handling
-   Structured logging
