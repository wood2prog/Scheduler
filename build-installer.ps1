#Requires -Version 5.1
<#
Publishes Scheduler as a self-contained win-x64 app and packages it into an MSI installer.
Output: installer\bin\x64\Release\SchedulerSetup.msi
#>
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot

Write-Host "Publishing Scheduler (self-contained, win-x64, $Configuration)..." -ForegroundColor Cyan
dotnet publish "$repoRoot\src\Scheduler\Scheduler.csproj" `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishReadyToRun=false `
    -p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

Write-Host "Building installer (MSI)..." -ForegroundColor Cyan
dotnet build "$repoRoot\installer\Scheduler.Installer.wixproj" -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Installer build failed." }

$msi = Get-ChildItem -Path "$repoRoot\installer\bin" -Filter "SchedulerSetup.msi" -Recurse | Select-Object -First 1
if ($msi) {
    Write-Host "Installer built: $($msi.FullName)" -ForegroundColor Green
} else {
    Write-Warning "Build succeeded but SchedulerSetup.msi was not found under installer\bin."
}
