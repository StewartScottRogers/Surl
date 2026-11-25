---
id: BL-1709
title: Withhold HTTP/2 cleartext credentials carried through a SOCKS tunnel from the exchange log
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1709 — Withhold HTTP/2 cleartext credentials carried through a SOCKS tunnel from the exchange log

## Goal

A SOCKS listener's levelled log never holds the `authorization` or `proxy-authorization` value of HTTP/2 sent in
cleartext through its tunnel, with prior knowledge or after an `h2c` upgrade.

## Context

- Found by the review of BL-1697; ADR-0182 Amendment 6 lists it as an accepted limit.
- The preface `PRI * HTTP/2.0` starts with a letter, so `SocksTunnelBytesRedactor` treats the tunnel as text; the
  credential travels as an HPACK field that no line rule sees.
- The SOCKS library may reference only what ADR-0002 decision 3's table allows. If it may not reference the HPACK
  library, fail closed: withhold every tunnelled byte after the preface as `****` and record that in an amendment.
- The HTTP proxy's `CONNECT` tunnel (ADR-0182 Amendment 2) may have the same gap: check it and file a task if so.

## Acceptance criteria

- [x] A named fast test in `Surl.Protocol.Socks.UnitTests` sends the HTTP/2 preface and an HPACK header block with a
      literal `authorization` through a granted tunnel and finds the credential's bytes nowhere in the logged output.
- [x] A named test does the same for the preface arriving after an HTTP/1.1 upgrade request and the origin's `101`.
- [x] ADR-0182 gains an amendment saying what is logged instead, and Amendment 6's HTTP/2 limit refers to it.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the SOCKS library stays at 100% line and branch
      coverage.

## Notes

- Tests (`SocksTunnelHttp2RedactionTests`): `Tunnel_Http2WithPriorKnowledge_WithholdsTheCredentialHeaderBlock` and
  `Tunnel_Http2AfterAnUpgradeAndThe101_WithholdsTheCredentialHeaderBlock`. Nine of the first set failed before the code.
- The SOCKS library may reference `Surl.Http2` (ADR-0002 decision 3), so it reuses `Http2RequestBytesRedactor`
  instead of withholding everything. Decision and limits: ADR-0182 Amendment 7.
- The preface is searched for anywhere, not only at a line start, because after an upgrade it follows the request
  body. Frames are read only for a preface that opens the tunnel or follows an origin `HTTP/1.1 101`; any other
  preface withholds the rest, because reading text as frames would log its secrets.
- Pipeline: the lane planned, wrote tests, implemented and documented itself (one small library, saves tokens); the
  review went to `code-reviewer`. Conformance was skipped: no byte Surl sends or accepts changed. No live curl run
  was made, so upstream curl's HTTP/2 through this tunnel is covered by the in-memory tests only.
- Review: fixed the `CreateRedactor` doc comment and the ADR's wording and limits, added two tests. Left as is: the
  preface search copies each chunk (linear cost, log path only). Help text is BL-1708's.
- `Measure-CodeQuality.ps1`: SOCKS library 100% line, 100% branch, 0 failing members. One earlier run of it stopped
  on a `dotnet test` failure whose test name was not captured; the next two test runs passed.
- Filed BL-1713 (the same gap in the HTTP proxy) and BL-1714 (`Http2RequestBytesRedactor` after an oversized header
  frame). `origin/factory/phase-1` already has another BL-1713, so the shift's `dedupe` will renumber this one.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. A SOCKS tunnel's log withholds HTTP/2 authorization and proxy-authorization, with prior knowledge and after an h2c upgrade
