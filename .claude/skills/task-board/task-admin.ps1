<#
.SYNOPSIS
    Reports on the Surl task board. Reads; never moves, creates or edits a task.

.DESCRIPTION
    task-board.ps1 owns every change of state, so that the Log and the completion
    dates stay honest. This script is its read-only counterpart: the listing,
    searching, inspection and validation a developer wants while deciding what to
    do, none of which should be able to move a file by accident. Nothing here
    writes to the board, which is why it needs no transition table and no -Reason.

    Commands:
      list    Tasks in one state folder, or all of them, with optional filters.
      show    One task in full, with its dependency and readiness position.
      find    Tasks whose title or body contains a string.
      deps    What a task waits on, and what waits on it, both transitively.
      check   Validate the board: duplicate or missing IDs, unknown field values,
              dependencies that do not exist, dependency cycles, finished tasks
              with unticked boxes, missing sections, non-ASCII bytes.
      report  Counts by state, assignee, pipeline and priority, and completions
              by date.

    Adding a command: add its name to the ValidateSet on $Command, write a
    function named Invoke-<Name> that takes the task list, and add one row to
    $Verbs at the foot of the file. See ADMIN.md.

    Keep this file ASCII only: Windows PowerShell 5.1 reads a script without a
    byte-order mark in the system code page, so a non-ASCII literal would be
    corrupted when it runs.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-admin.ps1 list -Folder Doing

.EXAMPLE
    ... task-admin.ps1 list -Folder Backlog -Assignee Claude -Pipeline protocol

.EXAMPLE
    ... task-admin.ps1 show -Id BL-008

.EXAMPLE
    ... task-admin.ps1 find -Text "exit 37"

.EXAMPLE
    ... task-admin.ps1 check
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('list', 'show', 'find', 'deps', 'check', 'report')]
    [string] $Command,

    # list: which state folder to read. 'All' spans every state, archives included.
    [ValidateSet('Backlog', 'Doing', 'Blocked', 'Deferred', 'Done', 'All')]
    [string] $Folder = 'All',

    [string] $Id,

    [string] $Text,

    [ValidateSet('High', 'Normal', 'Low')]
    [string] $Priority,

    [ValidateSet('Claude', 'Stewart')]
    [string] $Assignee,

    [ValidateSet('feature', 'protocol', 'docs', 'direct')]
    [string] $Pipeline,

    # list: leave archived tasks out, so a long history does not drown the board.
    [switch] $NoArchive,

    # check: return exit code 1 when anything is wrong, for a hook or a build step.
    [switch] $FailOnProblem
)

$ErrorActionPreference = 'Stop'

$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
if ($env:CLAUDE_PROJECT_DIR) { $RepoRoot = $env:CLAUDE_PROJECT_DIR }
else { $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path }
$Board = [IO.Path]::GetFullPath((Join-Path $RepoRoot 'Tasks'))
$DoneFolder = Join-Path $Board 'Done'
$States = @('Backlog', 'Doing', 'Blocked', 'Deferred', 'Done')
$PriorityRank = @{ 'High' = 0; 'Normal' = 1; 'Low' = 2 }
$Pipelines = @('feature', 'protocol', 'docs', 'direct')
$Assignees = @('Claude', 'Stewart')
$RequiredSections = @('Goal', 'Context', 'Acceptance criteria', 'Notes', 'Log')

if (-not (Test-Path -LiteralPath $Board)) { throw "Task board not found at $Board." }

function Read-Text([string] $Path) { return [IO.File]::ReadAllText($Path, $Utf8NoBom) }

# Front matter is parsed the same way task-board.ps1 parses it, deliberately: two
# readers that disagree about what a task says would be worse than no second reader.
function ConvertTo-Task([IO.FileInfo] $File) {
    $text = Read-Text $File.FullName
    $fields = @{}
    $header = [regex]::Match($text, '(?s)\A\s*---\r?\n(.*?)\r?\n---')
    if ($header.Success) {
        foreach ($line in ($header.Groups[1].Value -split '\r?\n')) {
            $pair = [regex]::Match($line, '^([A-Za-z][A-Za-z-]*):\s*(.*)$')
            if ($pair.Success) { $fields[$pair.Groups[1].Value] = $pair.Groups[2].Value.Trim() }
        }
    }

    $relative = $File.FullName.Substring($Board.Length).TrimStart('\', '/')
    $state = ($relative -split '[\\/]')[0]

    $taskId = [string]$fields['id']
    if (-not $taskId) { $taskId = [regex]::Match($File.Name, '^BL-\d+').Value }

    $dependencies = @()
    if ($fields['depends-on']) {
        $dependencies = @([regex]::Matches($fields['depends-on'], 'BL-\d+') | ForEach-Object { $_.Value })
    }

    $boxesTotal = @([regex]::Matches($text, '(?m)^\s*-\s\[[ xX]\]')).Count
    $boxesOpen = @([regex]::Matches($text, '(?m)^\s*-\s\[ \]')).Count

    return [pscustomobject]@{
        Id          = $taskId
        Number      = [int]($taskId -replace '\D', '')
        Title       = [string]$fields['title']
        Priority    = [string]$fields['priority']
        Assignee    = [string]$fields['assignee']
        Pipeline    = [string]$fields['pipeline']
        Requirement = [string]$fields['requirement']
        DependsOn   = $dependencies
        Created     = [string]$fields['created']
        Completed   = [string]$fields['completed']
        State       = $state
        Archived    = ($state -eq 'Done') -and ($File.DirectoryName -ne $DoneFolder)
        BoxesTotal  = $boxesTotal
        BoxesOpen   = $boxesOpen
        HasHeader   = $header.Success
        Path        = $File.FullName
        Relative    = 'Tasks\' + $relative
        Text        = $text
    }
}

function Get-Tasks {
    $files = @(Get-ChildItem -Path $Board -Recurse -File -Filter 'BL-*.md')
    $tasks = @($files | ForEach-Object { ConvertTo-Task $_ } | Where-Object { $States -contains $_.State })
    return , @($tasks | Sort-Object Number)
}

function Find-One([object[]] $Tasks, [string] $TaskId) {
    if (-not $TaskId) { throw 'Name the task with -Id, for example -Id BL-008.' }
    $wanted = $TaskId.Trim().ToUpperInvariant()
    $found = @($Tasks | Where-Object { $_.Id -eq $wanted })
    if ($found.Count -eq 0) { throw "There is no task $wanted on the board." }
    $live = @($found | Where-Object { -not $_.Archived })
    if ($live.Count -gt 0) { return $live[0] }
    return $found[0]
}

function Get-DoneIds([object[]] $Tasks) {
    return @($Tasks | Where-Object { $_.State -eq 'Done' } | ForEach-Object { $_.Id })
}

function Get-MissingDependencies($Task, [string[]] $DoneIds) {
    return @($Task.DependsOn | Where-Object { $DoneIds -notcontains $_ })
}

# Ready is defined exactly as task-board.ps1 defines it, so 'list -Folder Backlog'
# and 'next' can never disagree about which task is takeable.
function Get-ReadyTasks([object[]] $Tasks) {
    $doneIds = Get-DoneIds $Tasks
    $ready = @($Tasks | Where-Object {
            $_.State -eq 'Backlog' -and
            $_.Assignee -eq 'Claude' -and
            @(Get-MissingDependencies $_ $doneIds).Count -eq 0
        })
    return , @($ready | Sort-Object -Property @{ Expression = { $PriorityRank[$_.Priority] } }, Number)
}

function Format-Row($Task, [string[]] $ReadyIds, [string[]] $DoneIds) {
    $note = ''
    if ($Task.State -eq 'Backlog') {
        $missing = Get-MissingDependencies $Task $DoneIds
        if ($missing.Count -gt 0) { $note = "  [waiting on $($missing -join ', ')]" }
        elseif ($Task.Assignee -eq 'Stewart') { $note = '  [needs Stewart]' }
        else {
            $position = [array]::IndexOf($ReadyIds, $Task.Id)
            if ($position -ge 0) { $note = "  [ready, #$($position + 1) in queue]" }
        }
    }
    elseif ($Task.State -eq 'Done' -and $Task.Completed) { $note = "  [completed $($Task.Completed)]" }
    elseif ($Task.BoxesTotal -gt 0) { $note = "  [$($Task.BoxesTotal - $Task.BoxesOpen)/$($Task.BoxesTotal) criteria]" }

    $flag = ''
    if ($Task.Archived) { $flag = ' (archived)' }

    return '  {0} {1,-6} {2,-7} {3}{4}{5}' -f `
        $Task.Id, $Task.Priority, $Task.Assignee, $Task.Title, $flag, $note
}

function Select-Filtered([object[]] $Tasks) {
    $selected = $Tasks
    if ($Folder -ne 'All') { $selected = @($selected | Where-Object { $_.State -eq $Folder }) }
    if ($NoArchive) { $selected = @($selected | Where-Object { -not $_.Archived }) }
    if ($Priority) { $selected = @($selected | Where-Object { $_.Priority -eq $Priority }) }
    if ($Assignee) { $selected = @($selected | Where-Object { $_.Assignee -eq $Assignee }) }
    if ($Pipeline) { $selected = @($selected | Where-Object { $_.Pipeline -eq $Pipeline }) }
    if ($Id) {
        $wanted = $Id.Trim().ToUpperInvariant()
        $selected = @($selected | Where-Object { $_.Id -eq $wanted })
    }
    return , @($selected)
}

function Invoke-List([object[]] $Tasks) {
    $readyIds = @(Get-ReadyTasks $Tasks | ForEach-Object { $_.Id })
    $doneIds = Get-DoneIds $Tasks
    $selected = Select-Filtered $Tasks

    $describe = $Folder
    if ($Folder -eq 'All') { $describe = 'All states' }
    $filters = @()
    if ($NoArchive) { $filters += 'no archive' }
    if ($Priority) { $filters += "priority $Priority" }
    if ($Assignee) { $filters += "assignee $Assignee" }
    if ($Pipeline) { $filters += "pipeline $Pipeline" }
    if ($Id) { $filters += "id $Id" }
    $suffix = ''
    if ($filters.Count -gt 0) { $suffix = ' - ' + ($filters -join ', ') }

    Write-Output "$describe ($($selected.Count))$suffix"
    if ($selected.Count -eq 0) { Write-Output '  (none)'; return }

    if ($Folder -eq 'All') {
        foreach ($state in $States) {
            $inState = @($selected | Where-Object { $_.State -eq $state })
            if ($inState.Count -eq 0) { continue }
            Write-Output ''
            Write-Output "${state}:"
            foreach ($task in $inState) { Write-Output (Format-Row $task $readyIds $doneIds) }
        }
    }
    else {
        foreach ($task in $selected) { Write-Output (Format-Row $task $readyIds $doneIds) }
    }
}

function Invoke-Show([object[]] $Tasks) {
    $task = Find-One $Tasks $Id
    $doneIds = Get-DoneIds $Tasks
    $missing = Get-MissingDependencies $task $doneIds
    $blocks = @($Tasks | Where-Object { $_.DependsOn -contains $task.Id } | ForEach-Object { $_.Id })

    Write-Output "$($task.Id)  $($task.Title)"
    Write-Output "  file        $($task.Relative)"
    Write-Output "  state       $($task.State)$(if ($task.Archived) { ' (archived, never change)' })"
    Write-Output "  priority    $($task.Priority)"
    Write-Output "  assignee    $($task.Assignee)"
    Write-Output "  pipeline    $($task.Pipeline)"
    Write-Output "  requirement $($task.Requirement)"
    Write-Output "  created     $($task.Created)"
    if ($task.Completed) { Write-Output "  completed   $($task.Completed)" }
    if ($task.DependsOn.Count -gt 0) {
        Write-Output "  depends-on  $($task.DependsOn -join ', ')"
        if ($missing.Count -gt 0) { Write-Output "  NOT DONE    $($missing -join ', ')" }
    }
    if ($blocks.Count -gt 0) { Write-Output "  blocks      $($blocks -join ', ')" }
    if ($task.BoxesTotal -gt 0) {
        Write-Output "  criteria    $($task.BoxesTotal - $task.BoxesOpen) of $($task.BoxesTotal) ticked"
    }
    if ($task.State -eq 'Backlog' -and $task.Assignee -eq 'Claude' -and $missing.Count -eq 0) {
        $readyIds = @(Get-ReadyTasks $Tasks | ForEach-Object { $_.Id })
        Write-Output "  ready       yes, #$([array]::IndexOf($readyIds, $task.Id) + 1) in queue"
    }
    Write-Output ''
    Write-Output $task.Text.TrimEnd()
}

function Invoke-Find([object[]] $Tasks) {
    if (-not $Text) { throw 'Give the text to search for with -Text.' }
    $selected = Select-Filtered $Tasks
    $hits = @($selected | Where-Object {
            $_.Title -like "*$Text*" -or $_.Text -like "*$Text*"
        })

    Write-Output "Matches for '$Text' ($($hits.Count))"
    if ($hits.Count -eq 0) { Write-Output '  (none)'; return }

    foreach ($task in $hits) {
        Write-Output ''
        Write-Output "  $($task.Id)  $($task.State)  $($task.Title)"
        $lineNumber = 0
        foreach ($line in ($task.Text -split '\r?\n')) {
            $lineNumber++
            if ($line -like "*$Text*") {
                Write-Output ('    {0,4}: {1}' -f $lineNumber, $line.Trim())
            }
        }
    }
}

function Get-Transitive([object[]] $Tasks, [string] $StartId, [switch] $Downstream) {
    $byId = @{}
    foreach ($task in $Tasks) { $byId[$task.Id] = $task }
    $seen = New-Object 'System.Collections.Generic.HashSet[string]'
    $order = @()
    $queue = New-Object 'System.Collections.Generic.Queue[string]'
    $queue.Enqueue($StartId)
    while ($queue.Count -gt 0) {
        $current = $queue.Dequeue()
        if ($Downstream) {
            $nextIds = @($Tasks | Where-Object { $_.DependsOn -contains $current } | ForEach-Object { $_.Id })
        }
        elseif ($byId.ContainsKey($current)) { $nextIds = @($byId[$current].DependsOn) }
        else { $nextIds = @() }
        foreach ($nextId in $nextIds) {
            if ($seen.Add($nextId)) { $order += $nextId; $queue.Enqueue($nextId) }
        }
    }
    return , $order
}

function Invoke-Deps([object[]] $Tasks) {
    $task = Find-One $Tasks $Id
    $byId = @{}
    foreach ($item in $Tasks) { $byId[$item.Id] = $item }

    Write-Output "$($task.Id)  $($task.Title)"

    $upstream = Get-Transitive $Tasks $task.Id
    Write-Output ''
    Write-Output "Waits on ($($upstream.Count)), transitively:"
    if ($upstream.Count -eq 0) { Write-Output '  (nothing)' }
    foreach ($depId in $upstream) {
        if ($byId.ContainsKey($depId)) {
            $dep = $byId[$depId]
            Write-Output ('  {0}  {1,-8} {2,-7} {3}' -f $dep.Id, $dep.State, $dep.Assignee, $dep.Title)
        }
        else { Write-Output "  $depId  MISSING - no such task on the board" }
    }

    $downstream = Get-Transitive $Tasks $task.Id -Downstream
    Write-Output ''
    Write-Output "Blocks ($($downstream.Count)), transitively:"
    if ($downstream.Count -eq 0) { Write-Output '  (nothing)' }
    foreach ($blockedId in $downstream) {
        $blocked = $byId[$blockedId]
        Write-Output ('  {0}  {1,-8} {2,-7} {3}' -f $blocked.Id, $blocked.State, $blocked.Assignee, $blocked.Title)
    }
}

function Get-DependencyCycles([object[]] $Tasks) {
    $byId = @{}
    foreach ($task in $Tasks) { $byId[$task.Id] = $task }
    $cycles = @()
    $state = @{}

    function Visit([string] $NodeId, [System.Collections.ArrayList] $Stack) {
        if ($state[$NodeId] -eq 'done') { return }
        if ($state[$NodeId] -eq 'open') {
            $from = [array]::IndexOf($Stack.ToArray(), $NodeId)
            if ($from -ge 0) {
                $script:cycleFindings += (($Stack.ToArray()[$from..($Stack.Count - 1)] + $NodeId) -join ' -> ')
            }
            return
        }
        $state[$NodeId] = 'open'
        [void]$Stack.Add($NodeId)
        if ($byId.ContainsKey($NodeId)) {
            foreach ($nextId in $byId[$NodeId].DependsOn) { Visit $nextId $Stack }
        }
        $Stack.RemoveAt($Stack.Count - 1)
        $state[$NodeId] = 'done'
    }

    $script:cycleFindings = @()
    foreach ($task in $Tasks) {
        $stack = New-Object System.Collections.ArrayList
        Visit $task.Id $stack
    }
    $cycles = @($script:cycleFindings | Sort-Object -Unique)
    return , $cycles
}

function Invoke-Check([object[]] $Tasks) {
    $problems = @()
    $doneIds = Get-DoneIds $Tasks
    $ids = @($Tasks | ForEach-Object { $_.Id })

    foreach ($group in @($Tasks | Group-Object Id | Where-Object { $_.Count -gt 1 })) {
        $where = ($group.Group | ForEach-Object { $_.Relative }) -join ', '
        $problems += "Duplicate ID $($group.Name): $where"
    }

    foreach ($task in $Tasks) {
        $at = $task.Relative

        if (-not $task.HasHeader) { $problems += "$at has no front matter." }
        if (-not $task.Id) { $problems += "$at has no id." }
        elseif ($task.Id -ne [regex]::Match([IO.Path]::GetFileName($task.Path), '^BL-\d+').Value) {
            $problems += "$at declares id $($task.Id), which does not match its file name."
        }
        if (-not $task.Title) { $problems += "$at has no title." }
        if (-not $PriorityRank.ContainsKey($task.Priority)) {
            $problems += "$at has priority '$($task.Priority)'; expected High, Normal or Low."
        }
        if ($Assignees -notcontains $task.Assignee) {
            $problems += "$at has assignee '$($task.Assignee)'; expected Claude or Stewart."
        }
        if ($Pipelines -notcontains $task.Pipeline) {
            $problems += "$at has pipeline '$($task.Pipeline)'; expected feature, protocol, docs or direct."
        }
        if (-not $task.Requirement) { $problems += "$at has no requirement field; use 'none' when there is none." }
        if ($task.Created -notmatch '^\d{4}-\d{2}-\d{2}$') {
            $problems += "$at has created '$($task.Created)'; expected yyyy-MM-dd."
        }

        foreach ($depId in $task.DependsOn) {
            if ($ids -notcontains $depId) { $problems += "$at depends on $depId, which is not on the board." }
            if ($depId -eq $task.Id) { $problems += "$at depends on itself." }
        }

        if ($task.State -eq 'Done') {
            if ($task.Completed -notmatch '^\d{4}-\d{2}-\d{2}$') {
                $problems += "$at is Done with completed '$($task.Completed)'; expected yyyy-MM-dd."
            }
            if ($task.BoxesOpen -gt 0) {
                $problems += "$at is Done with $($task.BoxesOpen) unticked acceptance box(es)."
            }
        }
        else {
            if ($task.Completed) { $problems += "$at is $($task.State) but carries completed $($task.Completed)." }
            $waiting = Get-MissingDependencies $task $doneIds
            if ($task.State -eq 'Doing' -and $waiting.Count -gt 0) {
                $problems += "$at is Doing while $($waiting -join ', ') is not Done."
            }
        }

        # Only worth saying while the work is still ahead: acceptance criteria are what
        # make a task verifiable, and a task already in Done cannot be improved by
        # adding boxes to it. Four migrated tasks have none.
        if ($task.BoxesTotal -eq 0 -and $task.State -ne 'Done') {
            $problems += "$at has no acceptance criteria checkboxes, so nothing about it is verifiable."
        }

        foreach ($section in $RequiredSections) {
            if (-not [regex]::IsMatch($task.Text, "(?m)^## $([regex]::Escape($section))\s*$")) {
                $problems += "$at is missing its '## $section' section."
            }
        }
        if (-not [regex]::IsMatch($task.Text, '(?s)## Log.*\z')) {
            $problems += "$at has content after '## Log'; Log is the last section."
        }

        # There is deliberately no ASCII check on a task file. The ASCII-only rule
        # belongs to the .ps1 files that Windows PowerShell 5.1 reads in the system
        # code page; task files are UTF-8 and every heading legitimately carries an
        # em dash, so checking for it flagged all 15 tasks and meant nothing.
    }

    $inDoing = @($Tasks | Where-Object { $_.State -eq 'Doing' })
    if ($inDoing.Count -gt 1) {
        $problems += "More than one task is Doing: $(($inDoing | ForEach-Object { $_.Id }) -join ', '). One per session."
    }

    foreach ($cycle in (Get-DependencyCycles $Tasks)) { $problems += "Dependency cycle: $cycle" }

    $numbers = @($Tasks | Where-Object { -not $_.Archived -or $true } | ForEach-Object { $_.Number } | Sort-Object -Unique)
    if ($numbers.Count -gt 0) {
        $gaps = @()
        for ($n = 0; $n -le ($numbers[-1]); $n++) {
            if ($numbers -notcontains $n) { $gaps += ('BL-{0:D3}' -f $n) }
        }
        if ($gaps.Count -gt 0) { $problems += "Gaps in the ID sequence: $($gaps -join ', '). IDs are never reused." }
    }

    Write-Output "Checked $($Tasks.Count) task(s) on the board."
    if ($problems.Count -eq 0) {
        Write-Output 'No problems found.'
        return
    }
    Write-Output ''
    Write-Output "$($problems.Count) problem(s):"
    foreach ($problem in $problems) { Write-Output "  $problem" }
    if ($FailOnProblem) { exit 1 }
}

function Invoke-Report([object[]] $Tasks) {
    $live = @($Tasks | Where-Object { -not $_.Archived })

    Write-Output "Board: $($Tasks.Count) task(s), $($live.Count) live, $($Tasks.Count - $live.Count) archived."

    Write-Output ''
    Write-Output 'By state:'
    foreach ($state in $States) {
        $count = @($Tasks | Where-Object { $_.State -eq $state }).Count
        Write-Output ('  {0,-9} {1}' -f $state, $count)
    }

    Write-Output ''
    Write-Output 'By assignee (unfinished only):'
    foreach ($who in $Assignees) {
        $count = @($Tasks | Where-Object { $_.Assignee -eq $who -and $_.State -ne 'Done' }).Count
        Write-Output ('  {0,-9} {1}' -f $who, $count)
    }

    Write-Output ''
    Write-Output 'By pipeline (unfinished only):'
    foreach ($how in $Pipelines) {
        $count = @($Tasks | Where-Object { $_.Pipeline -eq $how -and $_.State -ne 'Done' }).Count
        Write-Output ('  {0,-9} {1}' -f $how, $count)
    }

    Write-Output ''
    Write-Output 'By priority (unfinished only):'
    foreach ($level in @('High', 'Normal', 'Low')) {
        $count = @($Tasks | Where-Object { $_.Priority -eq $level -and $_.State -ne 'Done' }).Count
        Write-Output ('  {0,-9} {1}' -f $level, $count)
    }

    $ready = Get-ReadyTasks $Tasks
    Write-Output ''
    Write-Output "Ready to start: $($ready.Count)"
    if ($ready.Count -gt 0) { Write-Output "  next: $($ready[0].Id)  $($ready[0].Title)" }

    $needsStewart = @($Tasks | Where-Object { $_.Assignee -eq 'Stewart' -and $_.State -ne 'Done' })
    Write-Output "Waiting on Stewart: $($needsStewart.Count)"
    foreach ($task in $needsStewart) { Write-Output "  $($task.Id)  $($task.Title)" }

    $completions = @($Tasks | Where-Object { $_.Completed } | Group-Object Completed | Sort-Object Name)
    if ($completions.Count -gt 0) {
        Write-Output ''
        Write-Output 'Completed by date:'
        foreach ($day in $completions) { Write-Output ('  {0}  {1}' -f $day.Name, $day.Count) }
    }
}

# One row per command. A new verb is this line plus its Invoke- function.
$Verbs = @{
    'list'   = { param($t) Invoke-List $t }
    'show'   = { param($t) Invoke-Show $t }
    'find'   = { param($t) Invoke-Find $t }
    'deps'   = { param($t) Invoke-Deps $t }
    'check'  = { param($t) Invoke-Check $t }
    'report' = { param($t) Invoke-Report $t }
}

$tasks = Get-Tasks
& $Verbs[$Command] $tasks
