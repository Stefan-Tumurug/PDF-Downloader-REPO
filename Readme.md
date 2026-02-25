PDF Downloader
Overview

PDF Downloader is a .NET console application that reads report metadata from an Excel dataset and downloads valid PDF reports to a specified output folder.

The system is designed with separation of concerns and a layered architecture (Core / CLI / Tests) to ensure maintainability and testability.

Features (Current Implementation – Day 1)

Read dataset from GRI_2017_2020.xlsx

Map rows to domain model (ReportRecord)

Download PDF files via HTTP

Validate PDF header (%PDF-)

Save valid files as <BRnum>.pdf

Skip invalid (HTML) responses

Clean Git setup with ignored build artifacts

Project Structure
PdfDownloader.Core      // Domain and business logic
PdfDownloader.Cli       // Console interface (composition root)
PdfDownloader.Tests     // Unit tests (planned)
How to Run
dotnet run --project ".\PdfDownloader.Cli\" -- "<path-to-xlsx>" "<output-folder>"

Example:

dotnet run --project ".\PdfDownloader.Cli\" -- "C:\data\GRI_2017_2020.xlsx" "C:\out"
Architecture

The application follows:

Separation of Concerns

Dependency Inversion Principle

Testable core logic

Explicit domain modeling

Excel parsing is handled via ClosedXML.

Next Steps

Move download orchestration into Core (DownloadRunner)

Implement status.csv output

Enforce maximum 10 successful downloads per run

Add unit tests