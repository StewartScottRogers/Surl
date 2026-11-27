---
id: BL-1876
title: Say in Surl.Conformance's CLAUDE.md what an upstream test-case server row's built flag means
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1876 — Say in Surl.Conformance's CLAUDE.md what an upstream test-case server row's built flag means

## Goal

`Conformance/Surl.Conformance.UnitLibrary/CLAUDE.md` describes the server table's `IsBuilt` flag as
its XML comment does.

## Context

Found during BL-1855 (2026-11-27). The "Selection is built" part of that file says each
`UpstreamTestCaseServer` row holds the scheme and "whether its server half is built". The `rtsp` and
`rtsp-ipv6` rows say built, `Surl.Protocol.Rtsp` has no upstream test-case server half, and the run
composes none (ADR-0080's 2026-11-27 amendment). `IsBuilt` in `UpstreamTestCaseServer.cs` now says
what the flag means.

## Acceptance criteria

- [x] The file's description of `UpstreamTestCaseServers`' rows is true of the `rtsp` and
      `rtsp-ipv6` rows.

## Notes

- 2026-11-27: `align-and-document` rewrote the first "Selection is built (BL-389)" bullet: a row
  holds `IsBuilt`, which lets a case naming the server past `server-not-built:<name>`, and true does
  not by itself mean the run composes a server half (`rtsp`, `rtsp-ipv6`). Checked against
  `UpstreamTestCaseServer.cs`, `UpstreamTestCaseServers.cs`,
  `UpstreamTestCaseSelector.FindServerNotBuilt` and ADR-0080's 2026-11-27 amendment.
- Markdown only, so no `verify` run; `dotnet build` (0 warnings) and the fast tests still ran.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. Surl.Conformance's CLAUDE.md now describes the server table's IsBuilt flag truthfully, rtsp and rtsp-ipv6 rows included
