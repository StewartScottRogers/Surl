---
id: BL-1982
title: Retry the next shift a -Continuous handover starts when it refuses because a lane is still running
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, Documentation/Wiki/Dark-Factory-Recipes.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1982 — Retry the next shift a -Continuous handover starts when it refuses because a lane is still running

## Goal

A `-Continuous` shift that ends with work still ready always leads to a running next shift,
even when one of its lanes is still finishing a task at the handover.

## Context

Shift `20261126-201542` (`Z:\repos\Surl.logs`): at 06:46:45 its coordinator logged "work is
still ready; next shift started (herdr tab w14:t1T)" (`RunDarkFactory.ps1`, near line 3417)
and closed its own tab. At 06:46:47 the new shift `20261127-064646` logged "refuse  lane 6 of
shift 20261126-201542 is still running BL-1815" (near line 3075) and exited 1. Lane 6 finished
BL-1815 at 06:56:56. Nothing retried, so no shift ran from 06:56 to 13:06, and the frozen merge
PR #159, green on every platform, waited six hours for a coordinator to merge it.

When a restart was tried at 13:06, the new shift also refused with "factory/phase-1 differs
from origin/factory/phase-1; push or pull first" because the main checkout was one commit
behind (only behind, with a clean tree). A fast-forward-only pull fixed it by hand.

Fix direction, for the implementer to settle: the handover waits for every lane of the ending
shift to stop before it starts the next one (or the next shift waits on the refusing lanes
rather than exiting), and a checkout that is only behind its remote with a clean working tree
is fast-forwarded rather than refused. Keep the refusal for a dirty tree or a branch that is
ahead or has diverged.

## Acceptance criteria

- [ ] When a `-Continuous` shift ends while one of its lanes still holds a task, the next shift
      starts once that lane stops, shown by a trace in this task's Notes (a short `-Hours` run
      or a `-DryRun` that simulates the held lane).
- [ ] The next shift's trace logs a `shift   start` line; it never ends with only a `refuse`
      line for a lane of the shift it replaces.
- [ ] A start in a checkout that is only behind its remote, with a clean working tree,
      fast-forwards and starts; a dirty or ahead or diverged checkout is still refused.
- [ ] `Documentation/Wiki/Dark-Factory-Recipes.md` and the script header describe the
      handover as it now behaves.

## Notes

## Log

- 2026-11-27: Created.
