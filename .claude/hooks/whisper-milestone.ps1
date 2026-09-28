<#
.SYNOPSIS
    Whispers to Stewart when a task reaches Done, a commit is made, or a branch is deleted.

.DESCRIPTION
    A PostToolUse hook for Bash and PowerShell tool calls, in every Claude Code session in
    this repository - the interactive one and every dark factory lane. It reads the hook's
    JSON from standard input, recognises three milestones from the command and its output,
    and speaks one short phrase very quietly in Windows' Zira voice:

      task-board.ps1 move ... -To Done        "Task done. B L 199, expand variable references in config files."
                                              (the task's whole file name, never shortened)
      git commit (that made a commit)         "Committed. Parse the proxy text."
      git branch -d/-D, git push --delete     "Branch factory BL 147 wip, deleted."

    Anything else is ignored. Phrases from several sessions queue behind a named mutex
    instead of talking over each other. The hook runs async, so no session waits for it,
    and it never fails a tool call: every error is swallowed.
#>
$ErrorActionPreference = 'Stop'
try {
    $hook = [Console]::In.ReadToEnd() | ConvertFrom-Json
    $command = "$($hook.tool_input.command)"
    $response = $hook.tool_response
    $output = "$($response.stdout)`n$($response.stderr)`n$($response.output)"
    $cwd = if ($hook.cwd) { "$($hook.cwd)" } else { (Get-Location).Path }

    function Get-Spoken([string]$Text) {
        # "BL-199" reads as "B L 199"; slashes, dashes and colons as pauses.
        $Text = $Text -replace '\bBL-(\d+)', 'B L $1'
        return ($Text -replace '[/_:]', ' ' -replace '\s+', ' ').Trim()
    }

    function Get-Words([string]$Text, [int]$Count) {
        return (($Text -split '\s+' | Where-Object { $_ } | Select-Object -First $Count) -join ' ')
    }

    $phrases = @()

    # A task moved to Done: the board script prints "BL-199  Doing -> Done  Tasks\Done\BL-199-<slug>.md".
    if ($command -match 'task-board\.ps1' -and $command -match '\bmove\b' -and $command -match '-To\s+Done') {
        foreach ($m in [regex]::Matches($output, '(BL-\d+)\s+\w+\s+->\s+Done\s+\S*?\1-([a-z0-9-]+)\.md')) {
            # The whole file name, never a shortened one: Stewart asked to hear all of it.
            $phrases += "Task done. $($m.Groups[1].Value), $($m.Groups[2].Value -replace '-', ' ')."
        }
    }

    # A commit: trust git, not the command line - the newest commit must be under a minute old.
    if ($command -match '\bgit\b[^|;&]*\bcommit\b' -and $command -notmatch '--dry-run') {
        $stamp = & git -C $cwd log -1 --format='%ct|%s' 2>$null
        if ($stamp -match '^(\d+)\|(.*)$') {
            $age = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds() - [long]$Matches[1]
            if ($age -ge 0 -and $age -le 60) {
                $subject = $Matches[2] -replace '^\w+(\([^)]*\))?!?:\s*', ''
                $phrases += "Committed. $(Get-Words $subject 7)."
            }
        }
    }

    # A branch deleted, locally ("Deleted branch x (was abc)") or on GitHub ("- [deleted]  x").
    if ($command -match '\bgit\b[^|;&]*\bbranch\b[^|;&]*\s-[dD]\b' -or $command -match '\bgit\b[^|;&]*\bpush\b[^|;&]*(--delete|\s:\S)') {
        $names = @([regex]::Matches($output, 'Deleted branch (\S+)') | ForEach-Object { $_.Groups[1].Value }) +
                 @([regex]::Matches($output, '-\s+\[deleted\]\s+(\S+)') | ForEach-Object { $_.Groups[1].Value })
        foreach ($name in ($names | Select-Object -Unique)) { $phrases += "Branch $name, deleted." }
    }

    if (-not $phrases.Count) { exit 0 }

    Add-Type -AssemblyName System.Speech
    $mutex = New-Object System.Threading.Mutex($false, 'Global\SurlWhisper')
    if (-not $mutex.WaitOne(120000)) { exit 0 }
    try {
        $voice = New-Object System.Speech.Synthesis.SpeechSynthesizer
        $voice.SetOutputToDefaultAudioDevice()
        try { $voice.SelectVoice('Microsoft Zira Desktop') } catch { }
        foreach ($phrase in $phrases) {
            $text = [System.Security.SecurityElement]::Escape((Get-Spoken $phrase))
            $voice.SpeakSsml("<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'><prosody volume='x-soft' rate='slow' pitch='low'>$text</prosody></speak>")
        }
        $voice.Dispose()
    } finally { $mutex.ReleaseMutex() }
} catch { }
exit 0
