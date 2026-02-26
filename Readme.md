# PDF Downloader
![CI](https://github.com/Stefan-Tumurug/PDF-Downloader-REPO/actions/workflows/ci.yml/badge.svg)

## Overview

PDF Downloader is a layered .NET application that reads report metadata from an Excel dataset and downloads valid PDF reports to a specified output folder.

The system is designed around separation of concerns and SOLID principles, allowing multiple user interfaces (CLI and GUI) to reuse the same orchestration logic.

Each run is intentionally limited to **10 successful downloads** to prevent excessive network usage.


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
- Stops after **10 successful downloads**


## Project Structure

| Project | Responsibility |
|--||
| **PdfDownloader.Core** | Domain logic and orchestration |
| **PdfDownloader.Cli** | Console interface (composition root) |
| **PdfDownloader.Gui** | WPF graphical interface |
| **PdfDownloader.Tests** | Unit tests with deterministic fakes |

The Core layer contains no UI or infrastructure concerns.


## How to Run

### CLI

Run without arguments:

```bash
dotnet run --project ".\PdfDownloader.Cli\"
```

Run with explicit arguments:

```bash
dotnet run --project ".\PdfDownloader.Cli\" -- "<path-to-xlsx>" "<output-folder>"
```

Example:

```bash
dotnet run --project ".\PdfDownloader.Cli\" -- "C:\data\GRI_2017_2020.xlsx" "C:\out"
```

Press **Ctrl+C** to cancel execution.



### GUI

Set `PdfDownloader.Gui` as startup project in Visual Studio.

The GUI allows:

- Browsing for Excel file
- Selecting output folder
- Choosing whether to overwrite existing files
- Cancelling downloads
- Viewing per-record results


## Output

The output folder will contain:

- PDF files named:

```
<BRnum>.pdf
```

- A generated:

```
status.csv
```

Each row describes:

- BR number
- Attempted URL
- Result status
- Error message (if applicable)



## Architecture

The system follows SOLID principles:

- `DownloadRunner` coordinates the workflow
- Infrastructure is abstracted via interfaces:
  - `IHttpDownloader`
  - `IFileStore`
  - `IStatusWriter`
- Core contains no HTTP or file system logic
- CLI and GUI act purely as presentation layers

The download workflow supports an **OverwriteExisting** option via:

```
DownloadOptions
```

This allows the UI layer to control behavior without changing domain logic.



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



## Documentation

Additional documentation is located in:

```
Opgaver/PDF Downloader/Documentation (Danish)
```



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
- Progress reporting
- Resume support
- Configurable download limit
