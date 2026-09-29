---
id: BL-035
title: Answer TELNET sessions in Surl.Protocol.Telnet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-005, BL-029]
touches: [Surl.Protocol.Telnet.UnitLibrary, Surl.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-035 — Answer TELNET sessions in Surl.Protocol.Telnet

## Goal

`Surl.Protocol.Telnet.UnitLibrary` contains a TELNET protocol server for the `telnet`
scheme. It completes the option negotiation the pinned upstream curl 8.21.0 build
performs, including the options curl's `-t`/`--telnet-option` sets, and then runs a
session whose behaviour the plan decides. It is proven by byte scripts recorded from
that build.

## Context

- TELNET is RFC 854 (protocol) and RFC 855 (options). Upstream curl's
  `-t`/`--telnet-option` sets `TTYPE`, `XDISPLOC` and `NEW_ENV`
  (https://curl.se/docs/manpage.html, checked 2026-09-28; the page then documented
  8.23.0). Which `IAC` sequences 8.21.0 actually sends, with and without `-t`, is
  measured with `Record-CurlExchange.ps1 -Raw` (BL-029) against the pinned build, never
  assumed. Use `-StandardInput` to give curl session input.
- Decide in the `/feature` plan, and state in the server's XML doc: which options the
  server agrees to (`DO`/`WILL`) and refuses, and what the session does with data (for
  example echo each line back and end on a fixed command). That makes the session
  predictable for conformance. `IAC IAC` escaping in data (RFC 854) is handled both ways.
- The server implements the protocol-server interface from the listener-seam ADR
  (BL-000, BL-005), and is tested through BL-005's in-memory connection. It references
  only Abstractions and ADR-0002's horizontal libraries.
- Fixtures: as in BL-017, under `Surl.Protocol.Telnet.UnitTests/Fixtures/<case>/`,
  embedded, with a `README.md`.

## Acceptance criteria

- [ ] Recordings exist for a plain session with standard input, and for a session with
      `-t TTYPE=vt100` and `-t NEW_ENV=USER,alice`. Each is fed Surl's intended bytes,
      and the pinned build's exit code and stdout are recorded.
- [ ] Fast tests replay each recording and assert Surl's negotiation and session bytes
      equal the accepted ones, and that received `TTYPE` and `NEW_ENV` values are
      reported through the exchange context.
- [ ] Fast tests cover `IAC IAC` in data both ways, an unknown option (refused with
      `DONT` or `WONT` as RFC 855 says), and a connection closed mid-sequence.
- [ ] `ProtocolIsolationTests` pass. `dotnet build Surl.Protocol.Telnet.UnitLibrary
      -warnaserror` is clean, the fast tests are green with no `Integration` test in
      `Surl.Protocol.Telnet.UnitTests`, and `Measure-CodeQuality.ps1` reports no failing
      member in `Surl.Protocol.Telnet.UnitLibrary`.

## Notes

Wiring `telnet` into `surl` and the live conformance run are BL-041.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
