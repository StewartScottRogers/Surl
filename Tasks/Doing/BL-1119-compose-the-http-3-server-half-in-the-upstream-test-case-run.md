---
id: BL-1119
title: Compose the http/3 server half in the upstream test-case run
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1117, BL-1118]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1119 — Compose the http/3 server half in the upstream test-case run

## Goal

The upstream test-case run composes the http/3 gateway half from BL-1117 on a UDP listener for
`%HTTP3PORT` with the `test-localhost` certificate, and marks `http/3` built. On a reference pin with
HTTP3, tests 2500 and 2503 then run instead of skipping.

## Context

- Read ADR-0154 (BL-1113) for the port, the certificate, the server-command row and whether
  `nghttpx-h3` is added to the selection rules. Without that feature, 2500 and 2503 skip
  `feature-missing:nghttpx-h3`.
- `Conformance/Surl.Conformance.UnitLibrary`:
  - flip `http/3` in `UpstreamTestCaseServers.All`;
  - add its `UpstreamTestCaseServerCommands` row;
  - add the feature if ADR-0154 says so. `HTTP3PORT` is already in `UpstreamTestCaseVariables`.
- `Conformance/Surl.Conformance.UnitTests`: compose the QUIC listener in `UpstreamTestCaseServerHalves`.
  Its datagram servers (`CaseDatagramServer`, used for TFTP and DNS) and the way
  `UpstreamCurlFetchesFromSurlOverHttp3Tests` starts HTTP/3 show how. Also remove `http/3` from the
  not-composed names in `UpstreamTestCasesAgainstSurlTests`.
- As in BL-1118, the Windows reference build has no HTTP3, so selection is proven by fast tests
  under the `linux-x64` pin's features, and BL-1121 records the CI run.

## Acceptance criteria

- [ ] `UpstreamTestCaseServers.Find("http/3")` reports built, and `UpstreamTestCaseServersTests`
      pins it.
- [ ] A fast test selects tests 2500 and 2503 under the `linux-x64` reference pin's features and
      asserts that neither is skipped. Under `win-x64` it asserts each skips `feature-missing:<name>`.
- [ ] `UpstreamTestCaseServerHalves` binds the http/3 UDP listener with the `test-localhost`
      certificate, and `%HTTP3PORT` carries its bound port.
- [ ] `dotnet build -warnaserror` is clean for both Conformance projects, and the fast tests are
      green. `Surl.Conformance.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
