---
id: BL-318
title: Prove pinned upstream curl completes RTSP requests with surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-317, BL-333, BL-337]
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-051
created: 2026-09-30
completed:
---
# BL-318 — Prove pinned upstream curl completes RTSP requests with surl

## Goal

`[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` prove that the pinned upstream
curl 8.21.0 builds complete RTSP requests against a live `surl` in every case BL-286's ADR lists, with
the exit codes it expects, on Windows and Linux; on macOS they report Inconclusive (ADR-0026).

## Context

- The cases and expected exit codes: BL-286's ADR's list (at least `OPTIONS` on a path, extra `-H`
  fields, `-u` with `--basic` refused over `rtsp://` and accepted with `--allow-plaintext-auth`, `-u`
  with `--digest`, `-T` with `OPTIONS`, `-v`, `-i`).
- If BL-285's or BL-286's ADR pinned the reference build's `libcurl-4.dll`, the libcurl-driven cases
  (`DESCRIBE`, `SETUP`, `PLAY` with the interleaved receive, `PAUSE`, `TEARDOWN`, the parameters,
  `ANNOUNCE`, `RECORD`) are proved here too through the driver the pin task built, reporting
  Inconclusive where the library is absent.
- ADR-0026 decision 2: the check is the pin's protocol list, written once beside
  `PinnedUpstreamCurl.RunAsync`; this is the first RTSP conformance task, so add it unless an earlier
  conformance task (BL-300 or BL-311) already did.
- Harness: `SurlOnLoopback.cs`, `PinnedUpstreamCurl.cs`, `AccountsFile.cs`, `DigestChallengeRelay.cs`.
- Any disagreement with the pinned build is fixed in the library at fault through a new task filed by
  `task-planner`, never by changing the expected result (ADR-0003); list them in the Log.

## Acceptance criteria

- [ ] Integration tests exist for every case of BL-286's ADR and pass on Windows with the pinned build
      present: `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [ ] Where the platform's pin lists no `rtsp`, the tests report Inconclusive with the pin named.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a socket.

## Notes

- Stewart approved lanes filing follow-up tasks beyond the plan (2026-09-30, "yes extra tasks"): a
  disagreement this task finds becomes its own task rather than widening this one.
- The libcurl cases and their expected results are ADR-0074 Amendment 1's last table (BL-333); every libcurl case sets `stream-uri:`. BL-337 builds the amended decision 5 those cases rely on.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
