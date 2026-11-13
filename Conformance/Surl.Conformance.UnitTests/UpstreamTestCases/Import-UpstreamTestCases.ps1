<#
.SYNOPSIS
    Imports upstream curl's tests/data and tests/certs from tag curl-8_21_0, byte for byte.

.DESCRIPTION
    Clones https://github.com/curl/curl at tag curl-8_21_0 (depth 1, sparse: tests/data,
    tests/certs, COPYING) with core.autocrlf=false and core.eol=lf, so the checkout holds the
    committed bytes. Refuses to copy unless HEAD is the tag's commit. Then replaces data/, certs/
    and COPYING beside this script and rewrites SHA256SUMS (ADR-0080 decision 1).

    Left out on purpose: tests/data/.gitignore (it would change what git ignores in Surl's tree),
    tests/data/Makefile.am (upstream's build glue), and everything in tests/certs but the .prm
    files, test-ca.cnf, and srp-verifier-db and srp-verifier-conf (the httptls+srp server half's
    accounts, ADR-0122 decision 7). It runs no curl: this is source text, not a build.

.PARAMETER ScratchDirectory
    An empty or missing directory to clone into. Defaults to a new folder under the temp path.
#>
[CmdletBinding()]
param(
    [string] $ScratchDirectory = (Join-Path ([System.IO.Path]::GetTempPath()) ("surl-curl-tests-" + [guid]::NewGuid().ToString('N')))
)

$ErrorActionPreference = 'Stop'

$tag = 'curl-8_21_0'
$expectedCommit = '68720b4837284335b2d63cb358f8f6ce65f5bc55'
$destination = $PSScriptRoot

if ((Test-Path $ScratchDirectory) -and (Get-ChildItem -Force $ScratchDirectory | Select-Object -First 1)) {
    throw "Scratch directory '$ScratchDirectory' is not empty."
}

$gitOptions = @('-c', 'core.autocrlf=false', '-c', 'core.eol=lf')
& git @gitOptions clone --depth 1 --branch $tag --no-checkout --filter=blob:none https://github.com/curl/curl $ScratchDirectory
if ($LASTEXITCODE -ne 0) { throw "git clone failed with exit code $LASTEXITCODE." }

& git -C $ScratchDirectory @gitOptions config core.autocrlf false
& git -C $ScratchDirectory @gitOptions sparse-checkout set --no-cone /tests/data/ /tests/certs/ /COPYING
if ($LASTEXITCODE -ne 0) { throw "git sparse-checkout failed with exit code $LASTEXITCODE." }
& git -C $ScratchDirectory @gitOptions checkout $tag
if ($LASTEXITCODE -ne 0) { throw "git checkout failed with exit code $LASTEXITCODE." }

$head = (& git -C $ScratchDirectory rev-parse HEAD).Trim()
if ($head -ne $expectedCommit) {
    throw "HEAD is $head, not $expectedCommit; refusing to import."
}

$sourceData = Join-Path $ScratchDirectory 'tests/data'
$sourceCerts = Join-Path $ScratchDirectory 'tests/certs'

$dataFiles = Get-ChildItem -LiteralPath $sourceData -File -Force |
    Where-Object { $_.Name -ne '.gitignore' -and $_.Name -ne 'Makefile.am' }
$certFiles = Get-ChildItem -LiteralPath $sourceCerts -File -Force |
    Where-Object { $_.Extension -eq '.prm' -or $_.Name -in 'test-ca.cnf', 'srp-verifier-db', 'srp-verifier-conf' }

foreach ($folder in 'data', 'certs') {
    $path = Join-Path $destination $folder
    if (Test-Path $path) { Remove-Item -LiteralPath $path -Recurse -Force }
    New-Item -ItemType Directory -Path $path | Out-Null
}

$imported = [System.Collections.Generic.List[string]]::new()
foreach ($file in $dataFiles) {
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $destination "data/$($file.Name)")
    $imported.Add("data/$($file.Name)")
}
foreach ($file in $certFiles) {
    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $destination "certs/$($file.Name)")
    $imported.Add("certs/$($file.Name)")
}
Copy-Item -LiteralPath (Join-Path $ScratchDirectory 'COPYING') -Destination (Join-Path $destination 'COPYING')
$imported.Add('COPYING')

$lines = $imported |
    ForEach-Object {
        $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $destination $_)).Hash.ToLowerInvariant()
        "$hash  $_"
    }
# Ordinal order, LF endings, no BOM, so the file reads the same on every platform.
$sorted = [string[]] $lines
[System.Array]::Sort($sorted, [System.StringComparer]::Ordinal)
[System.IO.File]::WriteAllText((Join-Path $destination 'SHA256SUMS'), (($sorted -join "`n") + "`n"), [System.Text.UTF8Encoding]::new($false))

Write-Host "Imported $($imported.Count) files from $tag ($expectedCommit): $($dataFiles.Count) data, $($certFiles.Count) certs, COPYING."
Write-Host "Scratch clone left at $ScratchDirectory."
