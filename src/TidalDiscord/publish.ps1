# Publishes TidalDiscord as a self-contained single-file
# Windows x64 executable into .\publish\TidalDiscord\

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "TidalDiscord.csproj"
$outDir  = Join-Path $PSScriptRoot "publish\TidalDiscord"

Write-Host "Publishing TidalDiscord (win-x64, self-contained, single file)..."

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishReadyToRun=true `
    -o $outDir

if ($LASTEXITCODE -ne 0)
{
    Write-Error "Publish failed."
    exit $LASTEXITCODE
}

# Keep the output directory clean — just the exe.
Remove-Item "$outDir\*.pdb" -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Published to: $outDir"
Write-Host "Executable:   $(Join-Path $outDir 'TidalDiscord.exe')"
Write-Host ""
Write-Host "NOTE: TIDAL API credentials live in .NET User Secrets"
Write-Host "and are NOT included in the published output."
