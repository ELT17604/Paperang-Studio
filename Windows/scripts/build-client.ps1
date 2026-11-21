param(
    [string]$Dotnet = 'dotnet',
    [ValidateSet('win-x64', 'win-arm64')][string]$Runtime = 'win-x64'
)
$ErrorActionPreference = 'Stop'
$windowsRoot = Split-Path $PSScriptRoot -Parent
$repositoryRoot = Split-Path $windowsRoot -Parent
$registry = Get-Content -LiteralPath (Join-Path $repositoryRoot 'VERSIONS.json') -Raw | ConvertFrom-Json
$workRoot = if ($env:PAPERANG_WIN_WORK_DIR) { $env:PAPERANG_WIN_WORK_DIR }
else { Join-Path (Split-Path $repositoryRoot -Parent) 'PaperangStudio-Windows-Development' }
$destination = Join-Path $workRoot "Release/$($registry.distributions.winclient.version)-$Runtime"
$project = Join-Path $windowsRoot 'src/Paperang.WinClient'
$verificationRuntime = 'win-' + [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString().ToLowerInvariant()
$verification = Join-Path $workRoot "Build/WinClientChecks/$verificationRuntime"
function Test-Desktop([string]$Directory, [string]$Argument, [string]$Name)
{
    $log = Join-Path $workRoot "Build/$Name"
    $process = Start-Process -FilePath (Join-Path $Directory 'Paperang.WinClient.exe') -ArgumentList $Argument -WindowStyle Hidden -PassThru -RedirectStandardOutput "$log.out" -RedirectStandardError "$log.err"
    if (!$process.WaitForExit(30000))
    {
        Stop-Process -Id $process.Id
        throw "$Name timed out"
    }
    if ($process.ExitCode -ne 0) { throw "${Name} failed: $(Get-Content -LiteralPath "$log.err" -Raw)" }
    Get-Content -LiteralPath "$log.out"
}
& $Dotnet publish $project -c Release -r $verificationRuntime --self-contained true -m:1 -p:PaperangDesktopChecks=true -o $verification
if ($LASTEXITCODE -ne 0) { throw 'Desktop verification build failed' }
Test-Desktop $verification '--self-test' 'winclient-selftest'
& $Dotnet publish $project -c Release -r $Runtime --self-contained true -m:1 -p:PaperangDesktopChecks=false -o $destination
if ($LASTEXITCODE -ne 0) { throw 'WinClient publish failed' }
foreach ($document in @('LICENSE', 'NOTICE'))
{
    Copy-Item -LiteralPath (Join-Path $repositoryRoot $document) -Destination $destination
}
# Only end-user guides are installed; developer inventories stay in source.
Copy-Item -LiteralPath (Join-Path $windowsRoot 'licenses') -Destination $destination -Recurse -Force
foreach ($obsolete in @('WINCLIENT.md', 'WINCLIENT.zh-CN.md', 'WINDOWS.md', 'WINDOWS.zh-CN.md', 'MAC-WINCLIENT-CONTROLS.zh-CN.md'))
{
    $oldGuide = Join-Path $destination $obsolete
    if (Test-Path -LiteralPath $oldGuide) { Remove-Item -LiteralPath $oldGuide }
}
foreach ($language in @('', '.zh-CN'))
{
    $guide = Get-Content -LiteralPath (Join-Path $repositoryRoot "doc/WINDOWS$language.md") -Raw
    $base = [Uri]'https://github.com/ELT17604/Paperang-Studio/blob/main/doc/'
    $guide = [regex]::Replace($guide, '\]\((?!https?://)([^)]+)\)', { param($match) '](' + [Uri]::new($base, $match.Groups[1].Value).AbsoluteUri + ')' })
    Set-Content -LiteralPath (Join-Path $destination "README$language.md") -Value $guide -Encoding utf8
}
foreach ($resource in @('App.xbf', 'Paperang.WinClient.pri', 'Microsoft.UI.Xaml.Controls.pri'))
{
    if (!(Test-Path -LiteralPath (Join-Path $destination $resource))) { throw "Missing WinUI resource: $resource" }
}
if (@('PaperangStudio.dll', 'PaperangStudio.exe', 'Paperang.WinLite.dll', 'Paperang.WinLite.exe') |
    Where-Object { Test-Path -LiteralPath (Join-Path $destination $_) })
{
    throw 'WinClient release contains a legacy application assembly; publish to a clean versioned directory'
}
$assembly = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes((Join-Path $destination 'Paperang.WinClient.dll')))
foreach ($fixture in @('CheckWorkspaceAsync', 'AdminLoginFixture', 'SessionPrinter'))
{
    if ($assembly.Contains($fixture)) { throw "Production application contains verification code: $fixture" }
}
if ($Runtime -eq $verificationRuntime) { Test-Desktop $destination '--smoke-test' 'winclient-production-smoke' }
else { Write-Warning 'Cross-built package: production startup must be checked on the target architecture' }
Write-Output "Built: $destination"
