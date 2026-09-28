# PostToolUse hook: format a C# file right after Claude edits or writes it.
# Claude Code passes a JavaScript Object Notation payload on standard input;
# tool_input.file_path is the file that was just touched.
# Always exits 0 so a formatting hiccup never blocks Claude.

try {
    $raw = [Console]::In.ReadToEnd() -replace '^[\uFEFF\u00EF\u00BB\u00BF\u2229\u2557\u2510]+', ''  # strip any byte-order mark
    $payload = $raw.Trim() | ConvertFrom-Json
    $file = $payload.tool_input.file_path
    if (-not $file -or $file -notmatch '\.cs$') { exit 0 }

    $root = if ($env:CLAUDE_PROJECT_DIR) { $env:CLAUDE_PROJECT_DIR } else { (Get-Location).Path }
    Set-Location $root

    $relative = (Resolve-Path -LiteralPath $file -Relative -ErrorAction Stop) -replace '^\.[\\/]', ''

    # Whitespace-only folder mode: fast, and works even before a solution file exists.
    dotnet format whitespace --folder --include $relative --verbosity quiet 2>&1 | Out-Null
}
catch {
    # Swallow errors: formatting is best-effort.
}
exit 0


