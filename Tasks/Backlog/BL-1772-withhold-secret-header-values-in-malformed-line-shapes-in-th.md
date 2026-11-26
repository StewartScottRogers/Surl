---
id: BL-1772
title: Withhold secret header values in malformed line shapes in the SOCKS tunnel's exchange log
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1760]
touches: [Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1772 — Withhold secret header values in malformed line shapes in the SOCKS tunnel's exchange log

## Goal

The SOCKS tunnel's exchange log withholds a secret header's value when the header line starts with a space or a tab, or
follows a bare carriage return.

## Context

- Found by the review of BL-1760 (ADR-0182 amendment 19, accepted limits). curl sends neither shape.
- `SocksTunnelPasswordAndCredentialHeaderLineRule` trims blanks only at the end of a header name, and a line ends only
  at a line feed (`SocksTunnelLinesRedactor`), so `" X-Api-Key: s"` with no withheld header before it, and
  `"X-Other: a\rX-Api-Key: s\r\n"`, are logged as received.
- Amendment 19's third shape, a folded line after a header line over 64 KiB, needs a change to `LineBytesRedactor`
  in the abstractions library and stays out of this task.
- Only the levelled log and trace dumps change; the event stream keeps raw bytes (ADR-0182 decision 4).

## Acceptance criteria

- [ ] A test in `SocksExchangeLogRedactionTests` logs `" X-Api-Key: s"` and `" Authorization: Basic s"` with the
      secret as `****`.
- [ ] A test there logs the bare carriage return input above with `s` withheld.
- [ ] A new ADR-0182 amendment and the rule's doc comment state the behaviour and which limits it closes.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch coverage.

## Notes

## Log

- 2026-11-25: Created.
