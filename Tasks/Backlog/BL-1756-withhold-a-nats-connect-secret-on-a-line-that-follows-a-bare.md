---
id: BL-1756
title: Withhold a NATS CONNECT secret on a line that follows a bare line feed
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-1756 — Withhold a NATS CONNECT secret on a line that follows a bare line feed

## Goal

The NATS exchange log writes `pass` and `auth_token` as `****` in a `CONNECT` line when the client ends lines with a
bare line feed.

## Context

- Found while working BL-1755. `NatsControlLineReader` ends a control line at LF, but `LineBytesRedactor` ends one
  only at CR LF, so `PING<LF>CONNECT {"pass":"<password>"}<CR><LF>` reaches `NatsConnectLineRedactionRule` as one
  line starting `PING` and is logged with the password. Read from the code; no test reproduces it yet.
- ADR-0182 decisions 1 and 6 set the rule. BL-1755 fixed the FTP rule by judging each LF-separated piece of the line
  it is handed, with no change to the shared `LineBytesRedactor`.

## Acceptance criteria

- [ ] A named test in `Surl.Extended.Protocol.Nats.UnitTests` shows `pass` and `auth_token` masked in a `CONNECT`
      line that follows a bare line feed.
- [ ] A named test shows every other line of the same chunk logged as sent, line feeds included.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
