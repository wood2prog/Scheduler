<img src="docs/readme-header.png" alt="Scheduler - simple project planning for real work" />

# Scheduler

A simple Windows desktop app for scheduling jobs. Each job has a name, a start date, an end date, and a completed flag. Jobs are listed in a sortable view, visualized as a Gantt chart, and persisted locally in SQLite.

## Tech stack

- WinForms UI
- Application/Services layer
- Domain/Core layer
- SQLite for storage

## Prerequisites

- Windows
- [.NET SDK 9.0+](https://dotnet.microsoft.com/download)

## Getting started

```
dotnet build
dotnet run --project src/Scheduler/Scheduler.csproj
```

On first run, the app creates a `scheduler.db` SQLite file next to the built executable and initializes the `Jobs` table automatically.

## Usage

- The main window has two tabs:
  - **List** — a grid of all jobs with their start date, end date, and a **Completed** checkbox.
  - **Calendar** — a Gantt chart with one row per job and one column per day. Each job is drawn as a colored bar spanning its start-to-end days; completed jobs render as a gray hatched bar instead. The date header and job-name column stay pinned while the grid scrolls horizontally/vertically.
- Use the **Sort by** dropdown to order both views by start date or end date.
- Enter a job name, click a start day and an end day on the inline calendars, and click **Add Job** to create a new job.
- Select a row in the List tab and click **Delete Job** to remove it (with a confirmation prompt).
- The window remembers its size and position between runs.

## Project structure

```
Scheduler.slnx
src/Scheduler/
  Domain/        Job entity
  Application/   IJobRepository, JobService (sorting/orchestration)
  Data/          SqliteJobRepository (SQLite persistence)
  UI/            MainForm (WinForms UI), GanttChartPanel (Gantt chart), WindowSettings (bounds persistence)
  Assets/        Application icon
  Program.cs     Composition root / app entry point
```

See [CLAUDE.md](CLAUDE.md) for a more detailed architecture and dependency-direction breakdown.
