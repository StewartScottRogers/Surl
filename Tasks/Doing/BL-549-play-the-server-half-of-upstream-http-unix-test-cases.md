---
id: BL-549
title: Play the server half of upstream http-unix test cases
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-547, BL-391, BL-393]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, Documentation/Conformance]
requirement: none
created: 2026-11-04
completed:
---
# BL-549 — Play the server half of upstream http-unix test cases

## Goal

The upstream test-case run covers cases whose `<server>` is `http-unix`: Surl's HTTP server half
(BL-391) answers on BL-547's Unix socket listener at `%HTTPUNIXPATH`, the `http-unix` row of the
server-name table in `Surl.Conformance.UnitLibrary` says built, and the pass-rate file counts the
cases.

## Context

- Decision: ADR-0080 decision 2 (the `http-unix` row, mode verbatim reply as `http`) and decision 3
  (`server-not-built:http-unix` stops applying the day the row flips); BL-571's ADR for the socket
  path.
- Cases that wait on this, tag `curl-8_21_0` (commit `68720b4837284335b2d63cb358f8f6ce65f5bc55`):
  1435 and 1436, feature `UnixSockets`, command `--unix-socket %HTTPUNIXPATH`. ADR-0016 says the
  Linux and macOS reference pins report `UnixSockets`; on a pin without it the cases skip
  `feature-missing:UnixSockets`.
- `Surl.Conformance.UnitTests` composes the server half as ADR-0080 decision 2 says: the HTTP
  server with BL-391's reply seam, on `Surl.Networking`'s listener. `%HTTPUNIXPATH` is a path in
  the case's log directory, short enough for `sun_path`.
- The run, checker and pass-rate file are BL-392's and BL-393's.

## Acceptance criteria

- [ ] The server-name table's `http-unix` row says built and maps `%HTTPUNIXPATH`, with a named
      test in `Surl.Conformance.UnitTests` that pins the row.
- [ ] The run covers 1435 and 1436 on each reference pin that has `UnixSockets`; the result lines
      list both by number with their outcome, and Notes record them per platform. A pin without the
      feature shows `feature-missing:UnixSockets`, not `server-not-built:http-unix`.
- [ ] A failing case is filed with `task-planner` as a new task grouped by cause, its ID in Notes.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and every touched library
      keeps 100% line and branch coverage.

## Notes

- If `Surl.Protocol.Http` needs a change to run on a Unix socket, that is a defect of BL-547's
  seam: file it as its own task rather than widening this one.

## Log

- 2026-11-04: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 1 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-549-lane-1-20261111-060243; start with git cherry-pick --no-commit factory/BL-549-lane-1-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
