---
id: BL-1917
title: Give a lane added mid-shift only the hours left in its shift
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, Documentation/Wiki/Dark-Factory-Recipes.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1917 — Give a lane added mid-shift only the hours left in its shift

## Goal

Every lane of a shift, including one `-Lanes Auto` adds or re-adds after the start, ends
at the shift's own end, so `-Hours` is the length of the shift and `-Continuous` starts the
next shift on time.

## Context

The coordinator launches each lane with `'-Hours', $Hours` (`RunDarkFactory.ps1`, the lane
argument list near line 3020), and the lane sets its own end as now plus `$Hours` (near
line 3012). A lane added later therefore runs a full `-Hours` from its own start.

Seen in shift `20261126-201542` (`Z:\repos\Surl.logs`), `-Hours 8`, shift end 04:16:
L1 to L3 ended at 04:16 to 04:29 ("time up"), but L5 started 21:49 "until 05:49", L4 was
re-added 22:20 "until 06:20" and L6 started 22:35 "until 06:35". From 04:25 the coordinator
logged "lanes 6 held (shift time up)" every 15 minutes while L4 to L6 kept claiming new
tasks (L6 claimed BL-1914 at 05:34), so the shift ran 2h19m past its end and the next
`-Continuous` shift could not start until then.

Fix direction: pass the hours left in the coordinator's `$shiftEnd`, for example
`'-Hours', [math]::Max(0.01, ($shiftEnd - (Get-Date)).TotalHours)`, keeping the existing
token-wait extension of `$shiftEnd` (near lines 3426 and 3436) consistent: a lane started
after a token wait must still end with the coordinator's extended end, not before it.
Check `-Restart` and the adopt-a-stopped-lane path pass the same remaining time.

## Acceptance criteria

- [x] The lane argument list passes the time left until the coordinator's `$shiftEnd`, not
      `$Hours`, for every lane start: the first lanes, a lane added by `-Lanes Auto`, a
      lane the coordinator restarts after its process died, and an adopted stopped lane.
- [x] A lane started mid-shift logs `shift   start ... until HH:mm` with the coordinator's
      shift end (within one minute), shown by a `-DryRun` or a short `-Hours` run whose
      trace is quoted in this task's Notes.
- [x] After the shift end, no lane logs a `claim` line; each lane finishes what it holds
      and logs `shift   end (time up)`.
- [x] `Documentation/Wiki/Dark-Factory-Recipes.md` and the script header say that `-Hours`
      bounds the whole shift, lanes added later included.

## Notes

What changed in `RunDarkFactory.ps1`:

- All four kinds of lane start go through `Start-Lane`, which now builds its arguments with
  `Get-LaneArguments -Until $shiftEnd`: `-Hours` is the hours left, never below 0.
- The coordinator adds each usage-limit wait to its own `$shiftEnd` and publishes the end
  as `lanes-<stamp>\shift-end.txt`. A lane reads it before every claim (`Test-ShiftTimeUp`).
- `Invoke-Claim` checks the end again after it gets the lock and syncs, because waiting for
  another lane's integration can outlast the last minutes.
- `-Restart` starts a new shift with a fresh end; its adopted lanes get the hours left in
  that new shift through the same `Start-Lane`.

Choices (defaults taken):

- One owner for the end. Adding each lane's own wait to its own end, as before, gave lanes
  different ends, some after the coordinator's. So a lane never moves its end.
- Consequence: in a lane shift, a wait for the API with no named usage limit
  (`Wait-ForTokensByProbe`) now counts against `-Hours`, because the coordinator cannot see
  it. A single-runner shift still gets that wait back.
- No `-DryRun` switch. `-TestShiftEnd` starts real lane processes with the coordinator's
  argument list; they stop after the start line, with no board, git or Claude.

Trace from `RunDarkFactory.ps1 -TestShiftEnd` (2026-11-27, all 5 checks PASS, exit 0). L1:
started with the shift ending 08:16. L2: the end extended to 09:16 after its arguments were
built. L3: started after the end.

```
05:59:16 L1 -   shift   start  branch=factory/rehearsal model=sonnet until 08:16
05:59:16 L1 -   shift   would claim; shift ends 08:16
05:59:18 L2 -   shift   start  branch=factory/rehearsal model=sonnet until 08:16
05:59:18 L2 -   shift   would claim; shift ends 09:16
05:59:19 L3 -   shift   start  branch=factory/rehearsal model=sonnet until 05:59
05:59:19 L3 -   shift   end (time up)
```

Not observed: a live multi-lane shift running past its end. Criterion 3 rests on the L3
rehearsal and on both claim paths calling `Test-ShiftTimeUp`.

`-TestRestart`, `-TestAutoLanes` and `-TestHeartbeat` exited 0. Build 0 warnings, format
clean, fast tests 30309 passed, 3 skipped.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. Every lane, including one added mid-shift, ends at the shift's own end and claims nothing after it
