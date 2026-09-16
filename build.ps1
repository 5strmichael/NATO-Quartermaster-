$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "NATO Quartermaster - SPT 4.1.x Builder"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET 10 SDK was not found. Install it from https://dotnet.microsoft.com/download/dotnet/10.0"
}

dotnet restore
dotnet build -c Release

Write-Host ""
Write-Host "Build complete. Installable ZIP:"
Write-Host "$PSScriptRoot\ReleaseZip\Michael-NATOQuartermaster-1.1.1.zip"
