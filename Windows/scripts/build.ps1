param(
    [string]$Dotnet = 'dotnet',
    [ValidateSet('win-x64', 'win-arm64')][string]$Runtime = 'win-x64',
    [switch]$SelfContained
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$flavor = if ($SelfContained) { 'portable' } else { 'lite' }
$repositoryRoot = Split-Path $projectRoot -Parent
$workRoot = if ($env:PAPERANG_WIN_WORK_DIR) { $env:PAPERANG_WIN_WORK_DIR } else { Join-Path (Split-Path $repositoryRoot -Parent) 'PaperangStudio-Windows-Development' }
$destination = Join-Path $workRoot "Release/V0.2.3-win_lite-$Runtime-$flavor"
$selfContainedValue = if ($SelfContained) { 'true' } else { 'false' }
& $Dotnet publish (Join-Path $projectRoot 'src/Paperang.WinLite') -c Release -r $Runtime --self-contained $selfContainedValue -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $destination
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE'),(Join-Path $repositoryRoot 'NOTICE'),(Join-Path $projectRoot 'README.md') -Destination $destination
$zip = "$destination.zip"
Compress-Archive -Path (Join-Path $destination '*') -DestinationPath $zip -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$zip.sha256" -Value "$hash  $(Split-Path $zip -Leaf)" -Encoding ascii
Write-Output "Built: $zip"
