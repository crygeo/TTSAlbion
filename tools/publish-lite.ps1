param(
    [string]$Configuration = "Release",
    [string]$TargetFramework = "net9.0-windows",
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "TTSAlbion\TTSAlbion.csproj"
$publishDir = Join-Path $repoRoot "publish-lite"

if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

dotnet publish $projectPath `
    -c $Configuration `
    -f $TargetFramework `
    -r $RuntimeIdentifier `
    -p:SelfContained=false `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:EnableCompressionInSingleFile=false `
    -p:PublishReadyToRun=true `
    -p:DeleteExistingFiles=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -p:PublishTrimmed=false `
    -o $publishDir

$logsDir = Join-Path $publishDir "Logs"
if (Test-Path $logsDir) {
    Remove-Item -LiteralPath $logsDir -Recurse -Force
}

$sizeBytes = (Get-ChildItem -LiteralPath $publishDir -Recurse -File | Measure-Object -Property Length -Sum).Sum
$sizeMb = [Math]::Round($sizeBytes / 1MB, 2)

Write-Host "Lite publish listo en: $publishDir"
Write-Host "Tamano aproximado: $sizeMb MB"
Write-Host "Requisitos: .NET Desktop Runtime 9 x64 y Python 3 con dependencias de requirements.txt"
