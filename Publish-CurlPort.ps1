<#
.SYNOPSIS
    Publishes the Curl port's curl executable from a named commit and pins it in
    CurlPortBuilds.json.

.DESCRIPTION
    The extended servers' Integration tests may connect with the Curl port's client, pinned
    by SHA-256 in CurlPortBuilds.json (ADR-0083 decision 2). This script makes that pin:

      1. resolves -Commit to a full 40-hex SHA of https://github.com/StewartScottRogers/Curl;
      2. fetches that one commit into its own source folder, <OutputRoot>\.source\<commit>,
         with a detached HEAD - never into Z:\repos\Curl or any other working tree of the
         port, which may have its own dark factory running in it;
      3. finds Curl.Console.csproj there and runs
         dotnet publish <project> -c Release -r <Platform> -o <OutputRoot>\<commit>
         from the source folder's root (native AOT; on Windows vswhere's folder,
         C:\Program Files (x86)\Microsoft Visual Studio\Installer, is put on PATH first);
      4. hashes the published curl executable with Get-FileHash, refuses it if that SHA-256
         is any UpstreamCurlBuilds.json entry's, and otherwise replaces CurlPortBuilds.json's
         entry for -Platform with {platform, path, sha256, commit, publishCommand, published};
      5. deletes the source folder.

    It never commits, pushes or edits a file in the port's repository, and it never commits
    CurlPortBuilds.json: re-pinning is an ordinary commit the caller makes.

.PARAMETER Commit
    The port commit to publish: a full SHA, an abbreviated SHA the remote advertises, or a
    branch name. Default: the current origin/master of https://github.com/StewartScottRogers/Curl,
    resolved with git ls-remote. The entry always records the full 40-hex SHA.

.PARAMETER Platform
    The .NET runtime identifier to publish for, and the entry's platform. Default win-x64.

.PARAMETER OutputRoot
    The folder each commit's output folder is made in. Default %LOCALAPPDATA%\Surl\curl-port
    on Windows and ~/.local/share/surl/curl-port elsewhere (ADR-0083 decision 2). With the
    default, the entry's path names %LOCALAPPDATA% (or %HOME%) rather than this account's
    profile folder, so the pin stays true on any account that publishes the same commit;
    with an explicit -OutputRoot the path is written literally.

.OUTPUTS
    Writes <OutputRoot>\<commit>\curl.exe (curl elsewhere) and CurlPortBuilds.json beside
    this script. Exits 0 when the entry was written, 1 when anything failed or was refused.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File Publish-CurlPort.ps1

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File Publish-CurlPort.ps1 -Commit 7051feb900be7ecb5b810eab587879abc5a5cffd
#>
[CmdletBinding()]
param(
    [string] $Commit = 'master',
    [string] $Platform = 'win-x64',
    [string] $OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$PortRepositoryUrl = 'https://github.com/StewartScottRogers/Curl'
$RepositoryRoot = $PSScriptRoot
$PinFilePath = Join-Path $RepositoryRoot 'CurlPortBuilds.json'
$UpstreamPinFilePath = Join-Path $RepositoryRoot 'UpstreamCurlBuilds.json'
$IsWindowsHost = [System.Environment]::OSVersion.Platform -eq [System.PlatformID]::Win32NT

function Invoke-Git {
    param([string[]] $Arguments)
    $output = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code ${LASTEXITCODE}: $output"
    }
    return $output
}

function Resolve-PortCommit {
    param([string] $Name)
    if ($Name -match '^[0-9a-fA-F]{40}$') {
        return $Name.ToLowerInvariant()
    }
    $advertised = Invoke-Git @('ls-remote', $PortRepositoryUrl)
    foreach ($line in $advertised) {
        $sha, $ref = "$line" -split "`t"
        if ($ref -eq "refs/heads/$Name" -or $ref -eq "refs/tags/$Name" -or $sha.StartsWith($Name.ToLowerInvariant())) {
            return $sha
        }
    }
    throw "$PortRepositoryUrl advertises no branch, tag or commit named '$Name'. Give the full 40-hex SHA."
}

function Get-DefaultOutputRoot {
    if ($IsWindowsHost) {
        return @{ Literal = Join-Path $env:LOCALAPPDATA 'Surl\curl-port'; Pinned = '%LOCALAPPDATA%\Surl\curl-port' }
    }
    return @{ Literal = Join-Path $HOME '.local/share/surl/curl-port'; Pinned = '%HOME%/.local/share/surl/curl-port' }
}

function ConvertTo-JsonString {
    param([string] $Text)
    $builder = [System.Text.StringBuilder]::new('"')
    foreach ($character in $Text.ToCharArray()) {
        switch ($character) {
            '"' { [void]$builder.Append('\"') }
            '\' { [void]$builder.Append('\\') }
            default {
                if ([int]$character -lt 0x20) { [void]$builder.AppendFormat('\u{0:x4}', [int]$character) }
                else { [void]$builder.Append($character) }
            }
        }
    }
    return $builder.Append('"').ToString()
}

function Write-PinFile {
    param([string] $Comment, [object[]] $Entries)
    $fieldNames = @('platform', 'path', 'sha256', 'commit', 'publishCommand', 'published')
    $entryTexts = foreach ($entry in $Entries) {
        $lines = foreach ($name in $fieldNames) { "      $(ConvertTo-JsonString $name): $(ConvertTo-JsonString $entry.$name)" }
        "    {`n" + ($lines -join ",`n") + "`n    }"
    }
    $text = "{`n  `"comment`": $(ConvertTo-JsonString $Comment),`n  `"builds`": [`n" + ($entryTexts -join ",`n") + "`n  ]`n}`n"
    [System.IO.File]::WriteAllText($PinFilePath, $text, [System.Text.UTF8Encoding]::new($false))
}

$fullCommit = Resolve-PortCommit $Commit
$defaultRoot = Get-DefaultOutputRoot
if ($OutputRoot) {
    $literalRoot = [System.IO.Path]::GetFullPath($OutputRoot)
    $pinnedRoot = $literalRoot
} else {
    $literalRoot = $defaultRoot.Literal
    $pinnedRoot = $defaultRoot.Pinned
}
$executableName = if ($Platform.StartsWith('win-')) { 'curl.exe' } else { 'curl' }
$outputFolder = Join-Path $literalRoot $fullCommit
$sourceFolder = Join-Path (Join-Path $literalRoot '.source') $fullCommit

if (Test-Path -LiteralPath $sourceFolder) {
    Remove-Item -LiteralPath $sourceFolder -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $sourceFolder | Out-Null

try {
    Write-Host "Fetching $fullCommit from $PortRepositoryUrl into $sourceFolder"
    Invoke-Git @('-C', $sourceFolder, 'init', '--quiet') | Out-Null
    Invoke-Git @('-C', $sourceFolder, 'fetch', '--quiet', '--depth', '1', $PortRepositoryUrl, $fullCommit) | Out-Null
    Invoke-Git @('-C', $sourceFolder, '-c', 'advice.detachedHead=false', 'checkout', '--quiet', '--detach', 'FETCH_HEAD') | Out-Null
    $checkedOut = "$(Invoke-Git @('-C', $sourceFolder, 'rev-parse', 'HEAD'))".Trim()
    if ($checkedOut -ne $fullCommit) {
        throw "Checked out $checkedOut, not $fullCommit."
    }

    $project = Get-ChildItem -LiteralPath $sourceFolder -Recurse -Filter 'Curl.Console.csproj' | Select-Object -First 1
    if (-not $project) {
        throw "Commit $fullCommit has no Curl.Console.csproj."
    }
    $projectRelative = $project.DirectoryName.Substring($sourceFolder.Length).TrimStart('\', '/')

    if ($IsWindowsHost) {
        $env:PATH = "C:\Program Files (x86)\Microsoft Visual Studio\Installer;$env:PATH"
    }
    $quotedOutput = if ($outputFolder.Contains(' ')) { "`"$outputFolder`"" } else { $outputFolder }
    $publishCommand = "dotnet publish $projectRelative -c Release -r $Platform -o $quotedOutput"
    Write-Host "Running in ${sourceFolder}: $publishCommand"
    Push-Location -LiteralPath $sourceFolder
    try {
        & dotnet publish $projectRelative -c Release -r $Platform -o $outputFolder
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed with exit code $LASTEXITCODE."
        }
    } finally {
        Pop-Location
    }
} finally {
    Remove-Item -LiteralPath $sourceFolder -Recurse -Force -ErrorAction SilentlyContinue
}

$executable = Join-Path $outputFolder $executableName
if (-not (Test-Path -LiteralPath $executable)) {
    throw "dotnet publish wrote no $executableName to $outputFolder."
}
$sha256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash.ToUpperInvariant()

$upstreamPins = Get-Content -Raw -LiteralPath $UpstreamPinFilePath | ConvertFrom-Json
$clash = @($upstreamPins.builds | Where-Object { $_.PSObject.Properties["sha256"] -and "$($_.sha256)".ToUpperInvariant() -eq $sha256 })
if ($clash.Count -gt 0) {
    throw "Refused: $executable has SHA-256 $sha256, which UpstreamCurlBuilds.json pins as upstream curl ($($clash[0].defaultPath)). A port client is never an upstream build (ADR-0003, ADR-0083)."
}

$pins = Get-Content -Raw -LiteralPath $PinFilePath | ConvertFrom-Json
$entries = @($pins.builds | Where-Object { $_.platform -ne $Platform })
$entries += [pscustomobject]@{
    platform       = $Platform
    path           = (@($pinnedRoot, $fullCommit, $executableName) -join $(if ($IsWindowsHost) { '\' } else { '/' }))
    sha256         = $sha256
    commit         = $fullCommit
    publishCommand = $publishCommand
    published      = (Get-Date).ToString('yyyy-MM-dd')
}
Write-PinFile -Comment $pins.comment -Entries ($entries | Sort-Object platform)

Write-Host "Pinned $Platform $executable (SHA-256 $sha256, commit $fullCommit) in $PinFilePath"
