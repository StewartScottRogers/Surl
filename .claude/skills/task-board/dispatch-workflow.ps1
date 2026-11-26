<#
.SYNOPSIS
    Dispatches a GitHub Actions workflow on the dark factory's shift branch for a task, and
    prints the run ID the task should wait on (BL-1342).

.DESCRIPTION
    A lane that needs a CI result it cannot produce on Windows (curl-port-phase-7, or ci.yml
    with attempts=2) dispatches it here instead of blocking for Stewart. The script:
      - refuses master and main: ci.yml's concurrency group would cancel master's own run;
      - refuses any ref that is not a pushed factory/* branch;
      - refuses a workflow that has no file under .github/workflows;
      - refuses a third dispatch for one task within -WindowHours (a shift), counted in
        <repo>.logs\workflow-dispatches.txt, so one task cannot start a run per claim;
      - dispatches with `gh workflow run <workflow> --ref <ref> -f name=value ...`;
      - finds the run it started and prints one line `run:<id> <workflow> <ref> <url>`.
    -DryRun runs every guard and prints what it would dispatch, without dispatching or counting.

    Waiting: the lane moves its task to Blocked with a -Reason containing `run:<id>` and no
    mention of Stewart. The coordinator (Invoke-Requeue in RunDarkFactory.ps1) polls
    `gh run view <id>` and moves the task back to Backlog when the run completes, with the
    run's conclusion in the Log. See Documentation/Wiki/Dark-Factory-Recipes.md.

.EXAMPLE
    dispatch-workflow.ps1 -Id BL-1341 -Workflow curl-port-phase-7 -Ref factory/phase-1
.EXAMPLE
    dispatch-workflow.ps1 -Id BL-1300 -Workflow ci -Ref factory/phase-1 -WorkflowInput attempts=2 -DryRun
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^BL-\d+$')][string]$Id,
    [Parameter(Mandatory)][string]$Workflow,
    [Parameter(Mandatory)][string]$Ref,
    [string[]]$WorkflowInput = @(),
    [int]$MaxPerTask = 2,
    [int]$WindowHours = 12,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'

function Get-DispatchRefusal {
    param([string]$Workflow, [string]$Ref, [string[]]$WorkflowInput, [string]$WorkflowDir)
    $name = $Workflow -replace '\.ya?ml$', ''
    if ($Ref -match '^(refs/heads/)?(master|main)$') { return "refused: $Ref is master; dispatch on the shift branch only" }
    if ($Ref -notmatch '^factory/[A-Za-z0-9._/-]+$') { return "refused: $Ref is not a factory/* shift branch" }
    if (-not ((Test-Path (Join-Path $WorkflowDir "$name.yml")) -or (Test-Path (Join-Path $WorkflowDir "$name.yaml")))) {
        return "refused: no workflow file $name.yml under .github/workflows"
    }
    foreach ($pair in $WorkflowInput) { if ($pair -notmatch '^[A-Za-z0-9_-]+=[^\r\n]*$') { return "refused: input '$pair' is not name=value" } }
    return ''
}

function Get-RecentDispatchCount {
    param([string]$CountFile, [string]$Id, [int]$WindowHours)
    if (-not (Test-Path $CountFile)) { return 0 }
    $since = (Get-Date).ToUniversalTime().AddHours(-$WindowHours)
    $n = 0
    foreach ($line in Get-Content $CountFile) {
        $parts = $line -split "`t"
        if ($parts.Count -ge 2 -and $parts[1] -eq $Id -and [datetime]::Parse($parts[0], [Globalization.CultureInfo]::InvariantCulture, 'AdjustToUniversal,AssumeUniversal') -ge $since) { $n++ }
    }
    return $n
}

# The newest run in `gh run list --json databaseId,createdAt,url` output created no earlier than
# 15 seconds before $StartedUtc, or nothing.
function Select-DispatchedRun {
    param([string]$RunListJson, [datetime]$StartedUtc)
    # Windows PowerShell 5.1 pipes a parsed JSON array on as one object; @() enumerates it only from a variable.
    $parsed = $RunListJson | ConvertFrom-Json
    $earliest = $StartedUtc.AddSeconds(-15)
    # 5.1 leaves createdAt as text, which casts to local time; pwsh parses it as a UTC DateTime.
    @($parsed) | Where-Object { $_ -and ([datetime]$_.createdAt).ToUniversalTime() -ge $earliest } |
        Sort-Object createdAt -Descending | Select-Object -First 1
}

$repo = (git rev-parse --show-toplevel).Trim()
$common = (git rev-parse --path-format=absolute --git-common-dir).Trim()
$mainRoot = Split-Path (Resolve-Path $common).Path -Parent
$countFile = "$mainRoot.logs\workflow-dispatches.txt"

$refusal = Get-DispatchRefusal -Workflow $Workflow -Ref $Ref -WorkflowInput $WorkflowInput -WorkflowDir (Join-Path $repo '.github\workflows')
if ($refusal) { Write-Error $refusal; exit 2 }

$used = Get-RecentDispatchCount -CountFile $countFile -Id $Id -WindowHours $WindowHours
if ($used -ge $MaxPerTask) { Write-Error "refused: $Id already dispatched $used workflow runs in the last $WindowHours hours (cap $MaxPerTask); read the earlier run instead"; exit 3 }

if (-not (git ls-remote --heads origin $Ref)) { Write-Error "refused: $Ref is not pushed to origin yet; the shift pushes lane commits when it integrates them, so wait for it or dispatch the branch that holds the work"; exit 4 }

$name = $Workflow -replace '\.ya?ml$', ''
$ghArgs = @('workflow', 'run', $name, '--ref', $Ref)
foreach ($pair in $WorkflowInput) { $ghArgs += @('-f', $pair) }
if ($DryRun) { Write-Output "dry-run: gh $($ghArgs -join ' ') (dispatch $($used + 1) of $MaxPerTask for $Id)"; exit 0 }

$started = (Get-Date).ToUniversalTime()
& gh @ghArgs
if ($LASTEXITCODE -ne 0) { Write-Error "gh workflow run failed with exit code $LASTEXITCODE"; exit 5 }
New-Item -ItemType Directory -Force (Split-Path $countFile) | Out-Null
Add-Content $countFile "$($started.ToString('o'))`t$Id`t$name`t$Ref"

for ($try = 0; $try -lt 12; $try++) {
    Start-Sleep -Seconds 5
    $json = (gh run list --workflow $name --branch $Ref --event workflow_dispatch --limit 5 --json databaseId,createdAt,url) -join "`n"
    $mine = Select-DispatchedRun -RunListJson $json -StartedUtc $started
    if ($mine) { Write-Output "run:$($mine.databaseId) $name $Ref $($mine.url)"; exit 0 }
}
Write-Error "dispatched $name on $Ref but could not find its run; read it with: gh run list --workflow $name --branch $Ref"
exit 6
