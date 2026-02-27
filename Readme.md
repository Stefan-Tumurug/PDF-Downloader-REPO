
# PDF Downloader
![CI](https://github.com/Stefan-Tumurug/PDF-Downloader-REPO/actions/workflows/ci.yml/badge.svg)

## Overview

PDF Downloader is a layered .NET application that reads report metadata from an Excel dataset and downloads valid PDF reports to a specified output folder.

The system is built around separation of concerns and SOLID principles.  
Both the CLI and GUI reuse the same orchestration logic from the Core layer.

Each run is intentionally limited to **a user-set amount of successful downloads** to prevent excessive network usage.

 

## Features

- Reads report metadata from Excel (`.xlsx`)
- Maps rows to domain objects (`ReportRecord`)
- Downloads PDFs using:
  - Primary URL
  - Fallback URL (if provided)
- Validates files using `%PDF-` header check
- Supports retry for transient HTTP failures
- Writes `status.csv` with:
  - BR number
  - Attempted URL
  - Status (Downloaded / Failed / SkippedExists)
  - Error message
- Continues execution even if individual downloads fail
- Supports cancellation (Ctrl+C in CLI / Cancel in GUI)
- Optional overwrite of existing output files
- Stops after **successful download limit is reached**
- Progress reporting in GUI via:
  - `DownloadProgress`
  - `SelectableReportRecord`
  - `MainViewModel`
  - `MainWindow.xaml` bindings

 

## Project Structure

| Project | Responsibility |
|---------|----------------|
| **PdfDownloader.Core** | Domain logic and orchestration |
| **PdfDownloader.Cli** | Console interface (composition root) |
| **PdfDownloader.Gui** | WPF graphical interface |
| **PdfDownloader.Tests** | Unit tests with deterministic fakes |

The Core layer contains no UI or infrastructure concerns.

 

## Sample Data

Sample input data is located in:

```
data/
```

 

## How to Run

### CLI

Run without arguments:

```
dotnet run --project ".\PdfDownloader.Cli\"
```

Run with explicit arguments:

```
dotnet run --project ".\PdfDownloader.Cli\" -- "<path-to-xlsx>" "<output-folder>"
```

Example:

```
dotnet run --project ".\PdfDownloader.Cli\" -- "data\GRI_2017_2020.xlsx" "out"
```

Press **Ctrl+C** to cancel execution.

 

### GUI

Set `PdfDownloader.Gui` as startup project in Visual Studio.

The GUI allows:

- Browsing for Excel file
- Selecting output folder
- Choosing whether to overwrite existing files
- Viewing per-record progress
- Cancelling downloads

Progress is reported through the `DownloadRunner` and surfaced via `DownloadProgress`
to the UI layer.

 

## Output

The output folder will contain:

PDF files named:

```
<BRnum>.pdf
```

And a generated:

```
status.csv
```

Each row describes:

- BR number
- Attempted URL
- Result status
- Error message (if applicable)

 

## Architecture

The system follows a layered architecture:

- Presentation layer (CLI / GUI)
- Core orchestration (`DownloadRunner`)
- Domain models
- Infrastructure implementations

`DownloadRunner` coordinates the workflow and depends only on abstractions:

- `IHttpDownloader`
- `IFileStore`
- `IStatusWriter`
- `IReportSource`

Infrastructure implementations provide the concrete behavior:

- `HttpClientDownloader`
- `LocalFileStore`
- `CsvStatusWriter`
- `ExcelReportSource`

Progress reporting is implemented through the `IProgress<DownloadProgress>` pattern,
allowing the Core layer to remain UI-agnostic.

## Testing

The project includes deterministic unit tests using MSTest.

External dependencies such as HTTP and file system access are replaced with test doubles to ensure predictable behavior.

### Covered Scenarios

The following core behaviors are verified:

- Successful PDF download using Primary URL
- Fallback URL is used when Primary fails
- Fallback is **not** used when Primary succeeds
- Files are skipped when they already exist (`OverwriteExisting = false`)
- Existing files are replaced when overwrite is enabled
- Execution stops after the configured maximum number of successful downloads
- Non-PDF responses are rejected
- Unsupported URL schemes are handled gracefully
- HTTP failures correctly fall back without retrying deterministic errors
- Status results are generated consistently

### Approach

Tests are built around:

- `FakeHttpDownloader`
- `InMemoryFileStore`
- `FakeStatusWriter`

This allows the `DownloadRunner` to be tested without network or disk access.

### Run Tests

```bash
dotnet test
```

## Documentation

Additional documentation is available in:

```
docs/
```

This includes:

- `architecture.png` — high-level layered architecture
- `domain-class-diagram.png` — detailed class relationships

 

## CI

GitHub Actions automatically runs:

- Restore
- Build
- Unit tests

on every push and pull request.

 

## Known Limitations

Some external links may fail due to:

- Dead hosts
- Redirects
- Forbidden access (403)
- HTML pages disguised as PDFs

These are expected and recorded in `status.csv`.

 

## Technology

- .NET
- C#
- WPF
- ClosedXML
- MSTest

 

## Design Goals

- Clean separation between UI and orchestration
- Testable domain logic
- Deterministic unit tests
- Extensible architecture

 

## Possible Future Improvements

- Better redirect handling
- Parallel downloads
- Resume support
