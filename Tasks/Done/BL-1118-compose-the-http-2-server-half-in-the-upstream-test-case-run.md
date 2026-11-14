---
id: BL-1118
title: Compose the http/2 server half in the upstream test-case run
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1115, BL-1116]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: FR-053
created: 2026-11-14
completed: 2026-11-14
---
# BL-1118 — Compose the http/2 server half in the upstream test-case run

## Goal

The upstream test-case run composes the http/2 gateway half from BL-1115 and BL-1116 on
`%HTTP2PORT` (plaintext) and `%HTTP2TLSPORT` (TLS) and marks `http/2` built. On a reference pin with
HTTP2, tests 1700, 1701, 1702 and 2403 then run instead of skipping `server-not-built:http/2`.

## Context

- Read ADR-0154 (BL-1113) for the ports, the certificate, the server-command row and whether the
  selection rules gain `nghttpx`.
- `Conformance/Surl.Conformance.UnitLibrary`:
  - flip `http/2` to `IsBuilt: true` in `UpstreamTestCaseServers.All`, naming its port variable as
    the other rows do;
  - add its row to `UpstreamTestCaseServerCommands`;
  - add `nghttpx` in `UpstreamTestCaseSelectionRules` or `UpstreamCurlBuildFeatures` if ADR-0154 says
    so. `HTTP2PORT` and `HTTP2TLSPORT` are already in `UpstreamTestCaseVariables`.
- `Conformance/Surl.Conformance.UnitTests`:
  - add the listeners to `UpstreamTestCaseServerHalves`' `Listeners` table and give their ports to
    the case variables;
  - remove `http/2` from the server names `UpstreamTestCasesAgainstSurlTests` writes as
    `server-half-not-composed`;
  - update `UpstreamTestCaseServersTests` (its `http/2` row is `false` today) and
    `UpstreamTestCaseServerCommandsTests`.
- The Windows reference build has no HTTP2 feature (`UpstreamCurlBuilds.json`), so a local Windows
  run skips these cases with `feature-missing`. They run on CI's Linux and macOS legs, and BL-1121
  records that run. Prove selection here with the fast selector tests, using the Linux reference
  pin's features.

## Acceptance criteria

- [x] `UpstreamTestCaseServers.Find("http/2")` reports built with its port variable, and
      `UpstreamTestCaseServersTests` pins it.
- [x] A fast test selects tests 1700, 1701, 1702 and 2403 under the `linux-x64` reference pin's
      features and asserts that none is skipped. Under the `win-x64` reference pin it asserts each
      skips `feature-missing:<name>`, never `server-not-built:http/2`.
- [x] `UpstreamTestCaseServerHalves` binds the http/2 plaintext and TLS listeners, and the case
      variables carry their bound ports.
- [x] `dotnet build -warnaserror` is clean for both Conformance projects, and the fast tests are
      green. `Surl.Conformance.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- ADR-0154 decision 6 followed: `http/2` built, `Http` command row, `nghttpx` feature when the pin has HTTP2.
- Choice: the gateway listeners use schemes `h2c` (plaintext, no TLS scheme) and `grpcs` (implicit TLS, ALPN exactly `h2`)
  because `http`/`https` are taken by other case servers and `Surl.Networking` is outside this task's `touches`. The
  `CaseServer` wrapper only keys a listener; the server it plays is the gateway either way.
- Windows reference pin skips: 1700-1702 `feature-missing:h2c`, 2403 `feature-missing:http/2`, never `server-not-built`.
- The live run (cases passing against pinned curl) happens on CI's Linux and macOS legs; BL-1121 records it.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. http/2 gateway half composed on HTTP2PORT and HTTP2TLSPORT; http/2 marked built, nghttpx feature added, cases 1700-1702 and 2403 select on Linux/macOS pins
