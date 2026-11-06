$ErrorActionPreference = "Stop"

if ($env:OS -ne "Windows_NT") {
    throw "Run NuGet updates on Windows so the lock files match the win-x64 Native AOT target."
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot

try {
    & dotnet package update --file moinsearch.slnx --verbosity minimal
    $packageUpdateExitCode = $LASTEXITCODE
    if ($packageUpdateExitCode -ne 0 -and $packageUpdateExitCode -ne 2) {
        throw "dotnet package update failed with exit code $LASTEXITCODE."
    }

    & dotnet restore moinsearch.slnx --force-evaluate
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore --force-evaluate failed with exit code $LASTEXITCODE."
    }

    & dotnet restore moinsearch.slnx --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore --locked-mode failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
