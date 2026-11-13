---
id: BL-1100
title: Pin upstream curl's macOS exit code for an HTTP/3-only GET when HTTP/3 is off
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests/UpstreamCurlFetchesFromSurlOverHttp3Tests.cs, Conformance/Surl.Conformance.UnitTests/CurlPortBesideUpstreamCurlOverHttp3Tests.cs, Documentation/Planning/Decisions/ADR-0082-how-surl-serves-http-3-over-quic.md]
requirement: none
created: 2026-11-13
completed:
---
# BL-1100 — Pin upstream curl's macOS exit code for an HTTP/3-only GET when HTTP/3 is off

## Goal

`UpstreamCurlFetchesFromSurlOverHttp3Tests.GetWithHttp3Only_Http3Off_FailsToConnect` passes on
CI's Windows, Linux and macOS jobs, pinning the exit code upstream curl 8.21.0 actually gives
on each, so the dark factory's merge to `master` is no longer held by it.

## Context

CI run 43398470597 (PR #27, frozen branch `factory/merge-20261112-162230`, commit ccf2a38):
"Build and test (macos-latest)" failed only this test; Windows and Ubuntu passed.

- Test: `Conformance/Surl.Conformance.UnitTests/UpstreamCurlFetchesFromSurlOverHttp3Tests.cs:191`
  asserts `OperatingSystem.IsWindows() ? 56 : 7`.
- macOS answered exit 55: `curl: (55) Failed to connect to 127.0.0.1:49962 after 2 ms: Failed sending data to the peer`.
- The comment above the test cites ADR-0082, "Measured": exit 56 on Windows, exit 7 on Linux
  and macOS. BL-768 (archived `Tasks/Done/2026-11-12_1408/`) saw this test green on macOS, so
  macOS answers 7 on some runs and 55 on others: with no UDP listener, the ICMP
  port-unreachable lands either on curl's connect check (7) or on its first send (55),
  depending on timing.
- `CurlPortBesideUpstreamCurlOverHttp3Tests.cs:176` mirrors this test; keep it consistent.
- Root `CLAUDE.md`, "Tests pass on Windows, Linux and macOS": where upstream curl's answer
  differs by platform, pin each platform's answer.

## Acceptance criteria

- [ ] On macOS the test accepts exit 7 or 55, since both are upstream curl's answer to no UDP listener, and on Linux it still requires 7. On Windows it still requires 56.
- [ ] The comment above the test, and ADR-0082's "Measured" section (as an amendment), state that macOS answers 7 or 55 and cite CI run 43398470597.
- [ ] `CurlPortBesideUpstreamCurlOverHttp3Tests.GetWithHttp3Only_Http3Off_FailsToConnect` still mirrors the upstream test.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green on Windows.

## Notes

Filed by an interactive session on 2026-11-13 after the macOS job held PR #27. Do not loosen
the Linux or Windows expectation. If the macOS failure turns out to be something else (not
the ICMP timing), say so in the ADR amendment and pin what was found instead.

## Log

- 2026-11-13: Created.
