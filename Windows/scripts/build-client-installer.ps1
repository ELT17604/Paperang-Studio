param([string]$Dotnet = 'dotnet', [string]$Iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $Iscc)) { throw 'Install Inno Setup 6 or pass -Iscc before building the installer' }
& "$PSScriptRoot/build-client.ps1" -Dotnet $Dotnet
$repository = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$version = (Get-Content -LiteralPath "$repository/VERSIONS.json" -Raw | ConvertFrom-Json).distributions.winclient.runtimeVersion
$work = if ($env:PAPERANG_WIN_WORK_DIR) { $env:PAPERANG_WIN_WORK_DIR }
else { Join-Path (Split-Path $repository -Parent) 'PaperangStudio-Windows-Development' }
$output = Join-Path $work 'Release'
$payload = Join-Path $output "v$version-win-x64"
& $Iscc "/DAppVersion=$version" "/DPayload=$payload" "/DOutputPath=$output" "$repository/Windows/installer/WinClient.iss"
if ($LASTEXITCODE -ne 0) { throw 'WinClient installer build failed' }
$installer = Join-Path $output "PaperangStudio-$version-win-x64-setup.exe"
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$installer.sha256" -Value "$hash  $(Split-Path $installer -Leaf)" -Encoding ascii
Write-Output "Installer: $installer"
