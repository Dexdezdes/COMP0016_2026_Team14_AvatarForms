# run-tests.ps1
# NOTE: arm64 is tried first because Windows on ARM machines run x64 processes
# (e.g. x64 Visual Studio) but still need the arm64 runtime DLL.
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"

$testBin = "AvatarFormsApp.Tests/bin/Debug/net10.0-windows10.0.19041.0"

# --- Find Windows App Runtime DLL ---
# Try arm64 first (Windows on ARM running x64 process still needs arm64 runtime),
# then fall back to x64, then x86.
Write-Host "Locating Windows App Runtime..." -ForegroundColor Cyan
$runtimeDll = $null
foreach ($arch in @("arm64", "x64", "x86")) {
    $runtimeDll = Get-ChildItem -Path "C:\Program Files\WindowsApps" `
        -Filter "Microsoft.WindowsAppRuntime.dll" -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\Microsoft\.WindowsAppRuntime\.\d+\.\d+_.*_${arch}__" } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if ($runtimeDll) {
        Write-Host "Found ($arch): $($runtimeDll.FullName)" -ForegroundColor DarkGray
        break
    }
}
if (-not $runtimeDll) {
    Write-Host "Could not find Microsoft.WindowsAppRuntime.dll." -ForegroundColor Red
    Write-Host "Install it with: winget install Microsoft.WindowsAppRuntime.1.7" -ForegroundColor Yellow
    exit 1
}

# --- Build ---
Write-Host "Building main project..." -ForegroundColor Cyan
msbuild AvatarFormsApp/AvatarFormsApp.csproj -nologo -verbosity:minimal
if ($LASTEXITCODE -ne 0) { Write-Host "Main project build failed." -ForegroundColor Red; exit 1 }

Write-Host "Building tests..." -ForegroundColor Cyan
msbuild AvatarFormsApp.Tests/AvatarFormsApp.Tests.csproj -nologo -verbosity:minimal
if ($LASTEXITCODE -ne 0) { Write-Host "Test build failed." -ForegroundColor Red; exit 1 }

# --- Copy runtime DLL ---
Write-Host "Copying Windows App Runtime to test bin..." -ForegroundColor Cyan
Copy-Item $runtimeDll.FullName $testBin -Force

# --- Run tests with coverage (collector) ---
Write-Host "Running tests with coverage (collector)..." -ForegroundColor Cyan

$resultsRoot = "coveragereport"
Remove-Item -Path $resultsRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $resultsRoot | Out-Null

# Run tests with coverage and filters to exclude generated code
dotnet test AvatarFormsApp.Tests/AvatarFormsApp.Tests.csproj `
    -c Debug `
    --results-directory $resultsRoot `
    --logger "trx;LogFileName=testresults.trx" `
    --collect:"XPlat Code Coverage" `
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura `
       DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Exclude="[*.g]*" `
       DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByAttribute="CompilerGenerated,GeneratedCodeAttribute"

# Capture exit code (0=all passed, non-zero=some tests failed)
$exitCode = $LASTEXITCODE

# Find the coverage file (coverlet/collector creates coverage.cobertura.xml in TestResults subfolder)
$coverageFile = Get-ChildItem -Path $resultsRoot -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $coverageFile) {
    Write-Host "ERROR: coverage.cobertura.xml not found under '$resultsRoot'." -ForegroundColor Red

    Write-Host "Listing $resultsRoot contents for diagnosis:" -ForegroundColor Yellow
    Get-ChildItem -Path $resultsRoot -Recurse | ForEach-Object {
        Write-Host $_.FullName
    }

    Write-Host ""
    Write-Host "Possible causes:" -ForegroundColor Yellow
    Write-Host "- coverlet.collector is not referenced in the test project (ensure package 'coverlet.collector' is installed)."
    Write-Host "- dotnet test used --no-build against out-of-date artifacts (avoid --no-build)."
    Write-Host "- Your exclusions removed all instrumented files (try running without exclusions to check)."
    Write-Host "- Tests run in a native-only host or instrumentation was skipped for platform-specific reasons."

    # Still attempt to run reportgenerator against anything that looks like a coverage file (optional)
    Write-Host "Aborting report generation." -ForegroundColor Red
    exit $exitCode
}

# Verify coverage file size > 0
if ($coverageFile.Length -le 0) {
    Write-Host "ERROR: coverage file exists but is empty: $($coverageFile.FullName)" -ForegroundColor Red
    exit $exitCode
}

Write-Host "Found coverage file: $($coverageFile.FullName)" -ForegroundColor Green

# --- Generate HTML report ---
Write-Host "Generating HTML report..." -ForegroundColor Cyan
$reportDir = Join-Path $resultsRoot "html"
reportgenerator `
    -reports:$($coverageFile.FullName) `
    -targetdir:$reportDir `
    -reporttypes:Html

# --- Open report ---
$reportIndex = Join-Path $reportDir "index.html"
if (Test-Path $reportIndex) {
    Write-Host "Done! Opening report..." -ForegroundColor Green
    Start-Process $reportIndex
} else {
    Write-Host "Report generation finished, but index.html not found. Check $reportDir" -ForegroundColor Yellow
}

# Exit with test result so CI knows whether tests failed
exit $exitCode