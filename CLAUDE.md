# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

All commands run from the repo root against `Scheduler.slnx`.

- Build: `dotnet build`
- Run: `dotnet run --project src/Scheduler/Scheduler.csproj`
- Restore packages: `dotnet restore`

- Test: `dotnet test` (xUnit project at `tests/Scheduler.Tests`, part of `Scheduler.slnx`).

Tests target the `Application`, `Data` and `Domain` layers directly, not the WinForms UI. Pure phase/report logic takes "today" as a parameter, so tests use a fixed date (`TestJobs.Origin`); `JobService` reads the real clock, so its tests build dates relative to `DateTime.Today`. `Data` tests run the real `SqliteJobRepository` against a throwaway temp file, including migrating older database schemas. The test project targets `net9.0-windows` because it references the WinForms project. When changing phase, report, pin, or schema logic, add or update tests alongside it.

The app is Windows-only (WinForms, `net9.0-windows`) and must be built/run on Windows.

### Building the installer

`.\build-installer.ps1` publishes `src/Scheduler/Scheduler.csproj` as a self-contained win-x64 build, then packages it into `installer\bin\x64\Release\SchedulerSetup.msi` via the WiX project at `installer/Scheduler.Installer.wixproj`. Requires the WiX v6 global tool (`dotnet tool install --global wix`) and its UI extension (`wix extension add WixToolset.UI.wixext`). `installer/Package.wxs` is intentionally not part of `Scheduler.slnx` — it depends on publish output existing first, which doesn't fit a plain `dotnet build` of the solution, so it's built as a separate step by the script. `installer/Scheduler.Installer.wixproj` relies on the WiX SDK's default `**/*.wxs` glob, so don't add an explicit `<Compile Include="Package.wxs" />` — that duplicates the source and fails the build with "Multiple entry sections" (WIX0089).

## Architecture

Single project (`src/Scheduler/Scheduler.csproj`, target `net9.0-windows`) organized into four layers by folder/namespace, per the spec in `Specs.txt`. There are no inter-project boundaries — layering is enforced by convention (namespace and one-way dependency direction), not by separate assemblies. Keep it that way; do not split into multiple projects unless the app's scope grows substantially.

Dependency direction: `UI -> Application -> Domain`, and `Data -> Application -> Domain`. `Domain` and `Application` have no dependency on `Data` or `UI`.

- **Domain/** (`Scheduler.Domain`) — `Job.cs`: the plain data class (Id, Name, StartDate, EndDate, Completed, pin flags, and the optional phase fields Phase/ConstructionStartDate/DeliveryStartDate/DeliveryTargetDate — see `docs/phases-plan.md`; `Phase` null means the job is unphased). No behavior, no dependencies on other layers.
- **Application/** (`Scheduler.Application`) — `IJobRepository.cs` (persistence abstraction), `JobService.cs` (sorting/orchestration logic, `JobSortOrder` enum), `JobPhases.cs` (phase rules: `JobStage`, changing stage, computing `PhaseSegment`s, seam moves) and `DurationReport.cs` (bell-curve report statistics). The UI and Data layers both depend on this layer; it depends on nothing else in the app.
- **Data/** (`Scheduler.Data`) — `SqliteJobRepository.cs`: the only SQLite-aware code. Implements `IJobRepository` using `Microsoft.Data.Sqlite` directly (no ORM). Creates the `Jobs` table on first connection if it doesn't exist (`CREATE TABLE IF NOT EXISTS`). Dates are stored as ISO 8601 strings (`DateTime.ToString("O")`).
- **UI/** (`Scheduler.UI`) — `MainForm.cs`: a single WinForms `Form` built entirely in code (no `.Designer.cs` partial split — controls are constructed directly in the constructor). Takes a `JobService` via constructor injection. Edits go through a draft copy of the job (`_draft`), not the controls directly. `GanttChartPanel.cs` draws the chart (phase segments, delivery marker, seam dragging); `ReportForm.cs`/`DurationReportPanel.cs` show the reports.
- **Program.cs** — the composition root. Wires up `SqliteJobRepository -> JobService -> MainForm` and starts the WinForms message loop. The SQLite file lives at `Documents/Scheduler/scheduler.db` (per-user, easy to find/back up — not next to the executable, which may be read-only once installed).

**Naming gotcha:** the `Scheduler.Application` namespace collides with `System.Windows.Forms.Application`. Any code with `using Scheduler.Application;` that also needs the WinForms `Application` class (e.g. `Application.Run(...)`) must fully qualify it as `System.Windows.Forms.Application`, as done in `Program.cs`.

When extending this app, keep new logic in the layer it belongs to: sorting/filtering/business rules go in `Application`, persistence details stay in `Data` behind `IJobRepository`, and `UI` should only call into `JobService` — it should not talk to `SqliteJobRepository` or raw SQL directly.
