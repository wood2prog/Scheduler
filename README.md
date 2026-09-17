# Scheduler

A simple Windows desktop app for scheduling jobs. Each job has a name, a start date, and an end date. Jobs are listed in a sortable view and persisted locally in SQLite.

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

- The main window lists all jobs with their start and end dates.
- Use the **Sort by** dropdown to order the list by start date or end date.
- Enter a job name, pick a start and end date, and click **Add Job** to create a new job.

## Project structure

```
Scheduler.slnx
src/Scheduler/
  Domain/        Job entity
  Application/   IJobRepository, JobService (sorting/orchestration)
  Data/          SqliteJobRepository (SQLite persistence)
  UI/            MainForm (WinForms UI)
  Program.cs     Composition root / app entry point
```

See [CLAUDE.md](CLAUDE.md) for a more detailed architecture and dependency-direction breakdown.
