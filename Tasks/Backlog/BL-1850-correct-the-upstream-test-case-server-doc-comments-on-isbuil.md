---
id: BL-1850
title: Correct the upstream test-case server doc comments on IsBuilt, adding a server half and the TLS-SRP listener
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary/UpstreamTestCaseServer.cs, Conformance/Surl.Conformance.UnitLibrary/UpstreamTestCaseServers.cs, Conformance/Surl.Conformance.UnitTests/UpstreamTestCaseTlsSrpServerHalf.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-1850 — Correct the upstream test-case server doc comments on IsBuilt, adding a server half and the TLS-SRP listener

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

- [ ] The `IsBuilt` comment is true of the `rtsp` and `rtsp-ipv6` rows.
- [ ] The `UpstreamTestCaseServers` summary agrees with ADR-0080's 2026-11-26 amendment on what
      building a server takes.
- [ ] The `UpstreamTestCaseTlsSrpServerHalf` remarks no longer say the TLS-SRP listener is unbuilt.

## Notes

## Log

- 2026-11-27: Created.
