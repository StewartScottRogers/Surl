---
id: BL-1855
title: Correct the upstream test-case server doc comments on IsBuilt, adding a server half and the TLS-SRP listener
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary/UpstreamTestCaseServer.cs, Conformance/Surl.Conformance.UnitLibrary/UpstreamTestCaseServers.cs, Conformance/Surl.Conformance.UnitTests/UpstreamTestCaseTlsSrpServerHalf.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1855 — Correct the upstream test-case server doc comments on IsBuilt, adding a server half and the TLS-SRP listener

## Goal

The XML comments on the upstream test-case server table and the TLS-SRP server half describe the
code as it is.

## Context

Found during BL-1835 (2026-11-27). Check each against the code before editing:

- `UpstreamTestCaseServer`'s `IsBuilt` is documented as "Whether Surl's server half for the name
  exists today", yet the `rtsp` and `rtsp-ipv6` rows are `true` and
  `UpstreamTestCasesAgainstSurlTests` composes no server half for either (ADR-0080's 2026-11-27
  amendment).
- `UpstreamTestCaseServers`' summary says "Building a server is one row's change", which ADR-0080's
  2026-11-26 amendment (BL-1808) says is not always so.
- `UpstreamTestCaseTlsSrpServerHalf`'s remarks say `Surl.Networking`'s TLS-SRP listener "is not
  built yet"; `ServerTlsSettings.ForTlsSrp` and `ServerTlsHandshake` serve TLS-SRP (BL-567).

Documentation only: no row's value and no behaviour changes.

## Acceptance criteria

- [x] The `IsBuilt` comment is true of the `rtsp` and `rtsp-ipv6` rows.
- [x] The `UpstreamTestCaseServers` summary agrees with ADR-0080's 2026-11-26 amendment on what
      building a server takes.
- [x] The `UpstreamTestCaseTlsSrpServerHalf` remarks no longer say the TLS-SRP listener is unbuilt.

## Notes

- `IsBuilt` is documented by what reads it: `UpstreamTestCaseSelector` skips
  `server-not-built:<name>` when it is false. The `rtsp` rows are true, `Surl.Protocol.Rtsp` has no
  upstream test-case server half and `ServerFamilies` lists no `rtsp` name.
- The TLS-SRP remarks give no reason for the half's own sockets beyond what the code shows; whether
  to move it behind `Surl.Networking`'s serving would be a behaviour change, outside this task.
- Verify (2026-11-27): build 0 warnings, format clean, fast tests 30,212 passed, 0 failed,
  3 skipped. Comment-only change, so no integration run.
- Follow-up filed: BL-1859 (the same wording in the library's `CLAUDE.md`).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The upstream test-case server and TLS-SRP server half doc comments describe the code as it is
