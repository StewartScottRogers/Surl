<#
.SYNOPSIS
    Installs Surl, the server-side mate of curl, on Windows from its GitHub release.

.DESCRIPTION
    Downloads the package for this machine's processor (x64 or arm64), checks it against
    the release's SHA256SUMS, and puts surl.exe in a directory of its own. -AddToPath also
    puts that directory on the user PATH. See DOWNLOAD.md.

    No release has been published yet; until one is, this script stops at "no release
    found".

.EXAMPLE
    irm https://raw.githubusercontent.com/StewartScottRogers/Surl/master/install.ps1 | iex
.EXAMPLE
    .\install.ps1 -Version v0.1.0 -AddToPath
#>
[CmdletBinding()]
param(
    # Release tag to install, e.g. v0.1.0. Default: the newest release, pre-releases included.
    [string]$Version = 'latest',
    # Where surl.exe goes.
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\surl\bin'),
    # Also add InstallDir to the user PATH.
    [switch]$AddToPath
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$repo = 'StewartScottRogers/Surl'
$arch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
switch ($arch) {
    'X64'   { $rid = 'win-x64' }
    'Arm64' { $rid = 'win-arm64' }
    default { throw "install.ps1: unsupported processor $arch; see DOWNLOAD.md" }
}
$package = "surl-$rid.zip"
# releases/latest skips pre-releases, so ask the API for the newest release of any kind.
if ($Version -eq 'latest') {
    # Windows PowerShell returns a JSON array as one object; the pipeline unrolls it.
    $newest = Invoke-RestMethod -UseBasicParsing -Uri "https://api.github.com/repos/$repo/releases?per_page=1" |
        ForEach-Object { $_ } | Select-Object -First 1
    if (-not $newest) { throw "install.ps1: no release found at https://github.com/$repo/releases" }
    $Version = $newest.tag_name
}
$base = "https://github.com/$repo/releases/download/$Version"

$tmp = Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetRandomFileName())
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
    Write-Host "Downloading $package ($Version)"
    Invoke-WebRequest -UseBasicParsing -Uri "$base/$package" -OutFile (Join-Path $tmp $package)
    Invoke-WebRequest -UseBasicParsing -Uri "$base/SHA256SUMS" -OutFile (Join-Path $tmp 'SHA256SUMS')

    $line = Get-Content (Join-Path $tmp 'SHA256SUMS') | Where-Object { $_ -match "\s$([regex]::Escape($package))$" }
    if (-not $line) { throw "install.ps1: $package is not listed in SHA256SUMS" }
    $expected = ($line -split '\s+')[0]
    $actual = (Get-FileHash (Join-Path $tmp $package) -Algorithm SHA256).Hash
    if ($expected -ne $actual) { throw "install.ps1: checksum mismatch for $package" }

    Expand-Archive -Path (Join-Path $tmp $package) -DestinationPath (Join-Path $tmp 'x')
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    Copy-Item (Join-Path $tmp 'x\surl.exe') (Join-Path $InstallDir 'surl.exe') -Force
} finally {
    Remove-Item -Recurse -Force $tmp
}

Write-Host "Installed $(Join-Path $InstallDir 'surl.exe')"
if ($AddToPath) {
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    if (($userPath -split ';') -notcontains $InstallDir) {
        [Environment]::SetEnvironmentVariable('Path', "$InstallDir;$userPath", 'User')
        Write-Host "Added $InstallDir to the user PATH (open a new terminal to pick it up)."
    }
}
