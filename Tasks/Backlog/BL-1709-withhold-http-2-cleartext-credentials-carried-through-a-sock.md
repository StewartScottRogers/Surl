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
completed:
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

- [ ] A named fast test in `Surl.Protocol.Socks.UnitTests` sends the HTTP/2 preface and an HPACK header block with a
      literal `authorization` through a granted tunnel and finds the credential's bytes nowhere in the logged output.
- [ ] A named test does the same for the preface arriving after an HTTP/1.1 upgrade request and the origin's `101`.
- [ ] ADR-0182 gains an amendment saying what is logged instead, and Amendment 6's HTTP/2 limit refers to it.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the SOCKS library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
