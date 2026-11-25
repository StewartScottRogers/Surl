# Rewrite this checkout's C# files that hold LF line endings so they hold CRLF.
# .editorconfig asks for CRLF, and `dotnet format --verify-no-changes` reports ENDOFLINE on a
# file a tool wrote with LF, because git does not rewrite it until it next checks the file out.
# Covers tracked files and untracked files that are not ignored; skips files marked -text.
# Only line breaks change: every other byte, a byte-order mark included, is kept.
# Prints each file it rewrites and the count. Run by the verify skill (SKILL.md beside this).

$ErrorActionPreference = 'Stop'

$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\..')).Path
$listing = git -C $root -c core.quotepath=off ls-files --cached --others --exclude-standard --eol -- '*.cs'
if ($LASTEXITCODE -ne 0) { throw "git ls-files failed with exit code $LASTEXITCODE." }

# Latin-1 maps each byte to one character and back, so the rewrite cannot change an encoding.
$bytesAsCharacters = [System.Text.Encoding]::GetEncoding(28591)
$rewritten = 0

foreach ($line in $listing) {
    $columns, $relativePath = $line -split "`t", 2
    if ($columns -notmatch '\sw/(lf|mixed)\s' -or $columns -match 'attr/-text') { continue }

    $path = Join-Path $root $relativePath
    $text = $bytesAsCharacters.GetString([System.IO.File]::ReadAllBytes($path))
    $converted = $text -replace '(?<!\r)\n', "`r`n"
    [System.IO.File]::WriteAllBytes($path, $bytesAsCharacters.GetBytes($converted))

    Write-Output "CRLF: $relativePath"
    $rewritten++
}

Write-Output "$rewritten C# file(s) rewritten from LF to CRLF."
