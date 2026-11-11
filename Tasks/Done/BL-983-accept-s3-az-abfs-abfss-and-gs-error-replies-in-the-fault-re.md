---
id: BL-983
title: Accept s3, az, abfs, abfss and gs error replies in the --fault reply check
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-980, BL-981, BL-982, BL-785]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: FR-062
created: 2026-11-06
completed: 2026-11-11
---
# BL-983 — Accept s3, az, abfs, abfss and gs error replies in the --fault reply check

## Goal

`surl --fault "error;scheme=s3;reply=503 SlowDown"` (and the same for `az`, `abfs`, `abfss` and `gs`) is read
instead of refused at configuration, now that each server answers a chosen error.

## Context

- Last of the four tasks BL-833 was split into; BL-980, BL-981 and BL-982 made the servers answer the rule.
- Add these schemes' reply shapes (ADR-0124's extended reply table: a status 400 to 599 and an error code word) to
  the `--fault` reply check BL-785 built in `Application/Surl.Cli.UnitLibrary`.
- Stand-in: the `--fault` option is how a command-line user injects the fault; `Surl.Api` and the control
  endpoint read the same rule text.

## Acceptance criteria

- [x] Named `Surl.Cli` tests: `--fault "error;scheme=s3;reply=503 SlowDown"` is read; `reply=200 OK` is refused;
      one accepted and one refused `reply` for `az` and for `gs`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Cli.UnitLibrary` keeps 100% line
      and branch coverage.

## Notes

- Added one shared rule, status 400 to 599 plus an error code word, for s3, az, abfs, abfss and gs in FaultReplyShapes (ADR-0124 decision 4). The two tests that used s3 as "no shape yet" now use nats.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. --fault error replies for s3, az, abfs, abfss and gs are read and checked
