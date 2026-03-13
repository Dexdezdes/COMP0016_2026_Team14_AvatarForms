# run-tests.ps1
$env:PATH += ";$env:USERPROFILE\.dotnet\tools"

$testBin = "AvatarFormsApp.Tests/bin/Debug/net10.0-windows10.0.19041.0"

# --- Find Windows App Runtime DLL ---
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

# --- Run tests with coverage ---
Write-Host "Running tests with coverage..." -ForegroundColor Cyan

$resultsRoot = "coveragereport"
Remove-Item -Path $resultsRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $resultsRoot | Out-Null

# Single combined exclude string for the XPlat collector
# Excludes:
#   - Auto-generated XAML/WinRT classes
#   - App.xaml.cs (XAML partial, blocks instrumentation — 30 seed tests prove it works)
#   - All Views except AvatarPage (XAML code-behind, untestable)
#   - Navigation/activation infrastructure (no business logic)
#   - Helpers with no testable logic
#   - DTOs, shell ViewModels, program entry point, SimpleWebServer
$exclude = "[AvatarFormsApp]AvatarFormsApp.AvatarFormsApp_XamlTypeInfo.*,[AvatarFormsApp]WinRT.*,[AvatarFormsApp]Microsoft.Windows.*,[AvatarFormsApp]AvatarFormsApp.App,[AvatarFormsApp]AvatarFormsApp.Views.CreateQuestionnairePage,[AvatarFormsApp]AvatarFormsApp.Views.QuestionnaireDetailPage,[AvatarFormsApp]AvatarFormsApp.Views.ResponseDetailPage,[AvatarFormsApp]AvatarFormsApp.Views.ResponsesPage,[AvatarFormsApp]AvatarFormsApp.Views.DashboardPage,[AvatarFormsApp]AvatarFormsApp.Views.ShellPage,[AvatarFormsApp]AvatarFormsApp.Views.MainWindow,[AvatarFormsApp]AvatarFormsApp.Services.NavigationService,[AvatarFormsApp]AvatarFormsApp.Services.NavigationViewService,[AvatarFormsApp]AvatarFormsApp.Services.PageService,[AvatarFormsApp]AvatarFormsApp.Services.ThemeSelectorService,[AvatarFormsApp]AvatarFormsApp.Services.LocalSettingsService,[AvatarFormsApp]AvatarFormsApp.Services.ActivationService,[AvatarFormsApp]AvatarFormsApp.Services.QuestionnaireAPIService,[AvatarFormsApp]AvatarFormsApp.Activation.*,[AvatarFormsApp]AvatarFormsApp.Behaviors.*,[AvatarFormsApp]AvatarFormsApp.Helpers.NavigationHelper,[AvatarFormsApp]AvatarFormsApp.Helpers.SettingsStorageExtensions,[AvatarFormsApp]AvatarFormsApp.Helpers.RuntimeHelper,[AvatarFormsApp]AvatarFormsApp.Helpers.ResourceExtensions,[AvatarFormsApp]AvatarFormsApp.Helpers.FrameExtensions,[AvatarFormsApp]AvatarFormsApp.WindowExtensions,[AvatarFormsApp]AvatarFormsApp.DTOs.*,[AvatarFormsApp]AvatarFormsApp.ViewModels.ShellPageViewModel,[AvatarFormsApp]AvatarFormsApp.ViewModels.AvatarPageViewModel,[AvatarFormsApp]AvatarFormsApp.Program,[AvatarFormsApp]SimpleWebServer"

dotnet test AvatarFormsApp.Tests/AvatarFormsApp.Tests.csproj `
    -c Debug `
    --results-directory $resultsRoot `
    --logger "trx;LogFileName=testresults.trx" `
    --collect:"XPlat Code Coverage" `
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura `
       DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Exclude="$exclude" `
       DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByAttribute="CompilerGenerated,GeneratedCodeAttribute"

$exitCode = $LASTEXITCODE

$coverageFile = Get-ChildItem -Path $resultsRoot -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $coverageFile) {
    Write-Host "ERROR: coverage.cobertura.xml not found under '$resultsRoot'." -ForegroundColor Red
    Get-ChildItem -Path $resultsRoot -Recurse | ForEach-Object { Write-Host $_.FullName }
    Write-Host "Aborting report generation." -ForegroundColor Red
    exit $exitCode
}

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

$reportIndex = Join-Path $reportDir "index.html"
if (Test-Path $reportIndex) {
    Write-Host "Done! Opening report..." -ForegroundColor Green
    Start-Process $reportIndex
} else {
    Write-Host "Report generation finished, but index.html not found. Check $reportDir" -ForegroundColor Yellow
}

exit $exitCode