---
id: BL-895
title: Close WebTransport connections for unmet requirements, refuse non-https schemes, hold early datagrams and drain through the shutdown grace
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-894, BL-460, BL-1035]
touches: [Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests, Documentation/Conformance/Extended/WebTransport.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-895 — Close WebTransport connections for unmet requirements, refuse non-https schemes, hold early datagrams and drain through the shutdown grace

## Goal

The WebTransport server does the four things of ADR-0117 that BL-459 could not with `Surl.Http3` as it was,
using what BL-894 adds.

## Context

- BL-459's Notes list the gaps; ADR-0117 decisions 0, 1, 3 and 6.
- Today (BL-459): unmet client SETTINGS reset the CONNECT stream `WT_REQUIREMENTS_NOT_MET` instead of closing
  the connection; `:scheme` is not checked; datagrams before their CONNECT are dropped by `Surl.Http3`; and at
  shutdown `WebTransportSession` sends `WT_DRAIN_SESSION`, `WT_CLOSE_SESSION` code 0 and FIN at once, before
  `GOAWAY`, with no grace.
- The rows of `Documentation/Conformance/Extended/WebTransport.md` naming BL-889.

## Acceptance criteria

- [ ] Named tests show: a client without `SETTINGS_H3_DATAGRAM` = 1 or `SETTINGS_WT_ENABLED` = 1 has its connection closed `WT_REQUIREMENTS_NOT_MET` (`0x212c0d48`) when it sends a WebTransport CONNECT; a CONNECT with `:scheme http` answered `:status 400` and FIN; a datagram sent before its CONNECT echoed once the session is accepted; at shutdown `WT_DRAIN_SESSION`, then `GOAWAY`, the session still echoing during the grace, then `WT_CLOSE_SESSION` code 0.
- [ ] Each matrix row naming BL-889 names its test instead.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- 2026-11-11: Not started. `dotnet build` fails with "A compatible .NET SDK was not found": `global.json` pins 10.0.401 and only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed. Added BL-1035 (Stewart reinstalls the SDK) to `depends-on`; the board restarts this task once it is Done.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035 - pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run
