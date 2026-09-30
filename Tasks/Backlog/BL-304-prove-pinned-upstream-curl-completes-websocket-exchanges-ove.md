---
id: BL-304
title: Prove pinned upstream curl completes WebSocket exchanges over ws and wss with surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-303]
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-048
created: 2026-09-30
completed:
---
# BL-304 — Prove pinned upstream curl completes WebSocket exchanges over ws and wss with surl

## Goal

`[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` prove that the pinned upstream
curl 8.21.0 builds complete WebSocket exchanges against a live `surl` over `ws` and `wss` in every
case BL-285's ADR lists, with the exit codes it expects, on Windows, Linux and macOS.

## Context

- The cases and expected exit codes: BL-285's ADR's list (at least the upgrade and the messages surl
  sends written by curl, `wss://` with `-k`, a refused upgrade (curl's 22), a login with each HTTP
  method the ADR offers, a `PING` curl answers, surl's `CLOSE`, the idle timeout).
- If BL-285's ADR decided to pin the reference build's `libcurl-4.dll`, the libcurl-driven cases
  (client text, binary, fragmented, ping and close frames) are proved here too, through the driver
  the pin task built; those cases report Inconclusive where the pinned library is absent.
- Harness: `SurlOnLoopback.cs`, `PinnedUpstreamCurl.cs`, `AccountsFile.cs`,
  `TestCertificateAuthority.cs`; `Assert.Inconclusive` when the pinned build is absent; the Linux and
  macOS legs run on CI (ADR-0016).
- Any disagreement with the pinned build is fixed in the library at fault through a new task filed by
  `task-planner`, never by changing the expected result (ADR-0003); list them in the Log.

## Acceptance criteria

- [ ] Integration tests exist for every case of BL-285's ADR and pass on Windows with the pinned build
      present: `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a socket.

## Notes

- Stewart approved lanes filing follow-up tasks beyond the plan (2026-09-30, "yes extra tasks"): a
  disagreement this task finds becomes its own task rather than widening this one.

## Log

- 2026-09-30: Created.
