---
id: BL-1237
title: Bring the Roadmap's shared-stores and hosts tracks up to date with the closed parents and Surl.Api's control endpoint
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1235]
touches: [Documentation/Planning/Roadmap.md]
requirement: none
created: 2026-11-15
completed:
---
# BL-1237 — Bring the Roadmap's shared-stores and hosts tracks up to date with the closed parents and Surl.Api's control endpoint

## Goal

`Documentation/Planning/Roadmap.md`'s "Track — Shared stores and the configuration command line" and
"Track — The hosts: Surl.Api and Surl.Mcp" state only what is true of the board and the code once BL-1235
has closed the Deferred parents.

## Context

Three statements in `Documentation/Planning/Roadmap.md` (line numbers as of 2026-11-15) are or will be
false (root `CLAUDE.md`, "Say what it does"):

- Shared-stores track, "Not built" (about line 255): "The deferred parents BL-653, BL-654, BL-663 and
  BL-666 were split ..." - after BL-1235 those parents are in `Done`, not deferred.
- Hosts track, "Status" (about line 264): "`--control`'s help text still says it is not served (BL-1125)"
  - BL-1125 is Done (archive `Tasks/Done/2026-11-14_1103/`); check the help text in
  `Application/Surl.Cli.UnitLibrary` and state what it says now.
- Hosts track, "Built since 2026-11-12" and "Not built" (about lines 278 to 286): "BL-816, which stays
  Deferred as the parent", "`Surl.Api` has no control-endpoint option" and "Not built: serving the control
  endpoint from `Surl.Api` (no task filed ...)" - BL-1176 (commit 19a743067) added
  `SurlServerOptions.ControlListenUrl`, `SurlServerOptions.ControlTokenFile` and `SurlServer.ControlUrl`
  (`Application/Surl.Api/SurlServerOptions.cs`, `SurlServer.cs`), and BL-1235 closes BL-816.

Change only these passages; keep each track's structure (Status, Delivers, Exit criteria, Built since,
Not built, Decisions).

## Acceptance criteria

- [ ] The shared-stores track names BL-653, BL-654, BL-663 and BL-666 as Done through their parts (BL-880 to
      BL-883, BL-987 to BL-995), not as deferred.
- [ ] The hosts track says `Surl.Api` serves the control endpoint through `SurlServerOptions.ControlListenUrl`
      and `ControlTokenFile` and reports `SurlServer.ControlUrl` (BL-1176), and no longer lists it as not built.
- [ ] The hosts track no longer says BL-816 stays Deferred or that `--control`'s help text says it is not
      served; whatever it says about the help text matches `Application/Surl.Cli.UnitLibrary` today.
- [ ] `Select-String -Path Documentation/Planning/Roadmap.md -Pattern 'stays Deferred','deferred parents'`
      finds nothing.

## Notes

- Filed by `task-planner` on 2026-11-15 while reviewing the Deferred tasks.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
