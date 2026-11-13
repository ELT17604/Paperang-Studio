param([string]$Dotnet = 'dotnet', [string]$Iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe")
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot/build.ps1" -Dotnet $Dotnet -SelfContained
$repository = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$version = (Get-Content "$repository/Windows/VERSION" -Raw).Trim()
$work = if ($env:PAPERANG_WIN_WORK_DIR)
{
	$env:PAPERANG_WIN_WORK_DIR
}
else
{
	Join-Path (Split-Path $repository -Parent) 'PaperangStudio-Windows-Development'
}
$output = Join-Path $work 'Release'
$payload = Join-Path $output "V$version-win-x64-portable"
& $Iscc "/DAppVersion=$version" "/DPayload=$payload" "/DOutputPath=$output" "$repository/Windows/installer/WinLite.iss"
if ($LASTEXITCODE -ne 0)
{
	throw 'Installer build failed'
}
$exe = Join-Path $output "PaperangStudio-$version-win-x64-setup.exe"
$hash = (Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content "$exe.sha256" "$hash  $(Split-Path $exe -Leaf)" -Encoding ascii
Write-Output "Installer: $exe"
