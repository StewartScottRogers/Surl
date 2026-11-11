---
id: BL-912
title: Normalise the client's ephemeral port in Phase 7's compared standard error lines
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1035]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests/CurlPortResultNormaliserTests.cs]
requirement: none
created: 2026-11-05
completed:
---
# BL-912 — Normalise the client's ephemeral port in Phase 7's compared standard error lines

## Goal

`CurlPortResultNormaliser.NormaliseStandardError` replaces every loopback port in a compared line (as
`NormaliseSurlNotes` already does), so a Phase 7 case can compare curl's `-v` line
`* Established connection to 127.0.0.1 (127.0.0.1 port N) from 127.0.0.1 port M`.

## Context

- Found in BL-589 (ADR-0089 amendment 4): `NormaliseStandardError` replaces only `surl`'s port, so the
  client's own ephemeral port `M` differs between any two runs, and
  `CurlPortBesideUpstreamCurlOverHttp2Tests.Get_TwoUrls_ReuseOneHttp2Connection` compares only the
  `* Reusing existing` line rather than both lines its mirrored test asserts on.
- `CurlPortResultNormaliser` in `Conformance/Surl.Conformance.UnitLibrary`; its `LoopbackPort` pattern
  is what `NormaliseSurlNotes` uses.
- Once done, a later task may add `* Established connection to ` back to that case's
  `AssertedStandardErrorFragments` (it is in BL-589's file, not this task's `touches`).

## Acceptance criteria

- [ ] A `CurlPortResultNormaliserTests` test shows `NormaliseStandardError` turning
      `* Established connection to 127.0.0.1 (127.0.0.1 port 5000) from 127.0.0.1 port 61234` into a line
      with both ports replaced by `<port>`.
- [ ] `Surl.Conformance.UnitLibrary` keeps 100% line and branch coverage.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- 2026-11-11 (lane 1): Change written (LoopbackPort applied to each kept stderr line in `NormaliseStandardError`, test `NormaliseStandardError_ReplacesTheClientsEphemeralPortBesideALoopbackAddress`, library CLAUDE.md line) but not verified: `dotnet build` fails because the .NET SDK 10.0.401 that global.json pins is not installed (only 10.0.100-rc.1, 11.0.x). Left uncommitted for the shift to stash; waits on BL-1035.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035 - pinned .NET SDK 10.0.401 is not installed, so the build cannot run
