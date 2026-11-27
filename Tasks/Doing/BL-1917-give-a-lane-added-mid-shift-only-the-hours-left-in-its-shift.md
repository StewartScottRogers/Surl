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
completed:
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

- [ ] The lane argument list passes the time left until the coordinator's `$shiftEnd`, not
      `$Hours`, for every lane start: the first lanes, a lane added by `-Lanes Auto`, a
      lane the coordinator restarts after its process died, and an adopted stopped lane.
- [ ] A lane started mid-shift logs `shift   start ... until HH:mm` with the coordinator's
      shift end (within one minute), shown by a `-DryRun` or a short `-Hours` run whose
      trace is quoted in this task's Notes.
- [ ] After the shift end, no lane logs a `claim` line; each lane finishes what it holds
      and logs `shift   end (time up)`.
- [ ] `Documentation/Wiki/Dark-Factory-Recipes.md` and the script header say that `-Hours`
      bounds the whole shift, lanes added later included.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
