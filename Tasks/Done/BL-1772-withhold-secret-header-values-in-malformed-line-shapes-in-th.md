---
id: BL-1772
title: Withhold secret header values in malformed line shapes in the SOCKS tunnel's exchange log
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1760, BL-1767]
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed: 2026-11-26
---
# BL-1772 — Withhold secret header values in malformed line shapes in the SOCKS tunnel's exchange log

## Goal

The SOCKS tunnel's exchange log withholds a secret header's value when the header line starts with a space or a tab, or
follows a bare carriage return.

## Context

- Found by the review of BL-1760 (ADR-0182 amendment 19, accepted limits). curl sends neither shape.
- `TunnelPasswordAndCredentialHeaderLineRule` trims blanks only at the end of a header name, and a line ends only
  at a line feed (`TunnelLinesRedactor`), so `" X-Api-Key: s"` with no withheld header before it, and
  `"X-Other: a\rX-Api-Key: s\r\n"`, are logged as received.
- BL-1767 moved both classes into `Surl.LineProtocol`, where the HTTP proxy uses them too, so a fix there changes the
  proxy's log as well.
- Amendment 19's third shape, a folded line after a header line over 64 KiB, needs a change to `LineBytesRedactor`
  in the abstractions library and stays out of this task.
- Only the levelled log and trace dumps change; the event stream keeps raw bytes (ADR-0182 decision 4).

## Acceptance criteria

- [x] A test in `SocksExchangeLogRedactionTests` logs `" X-Api-Key: s"` and `" Authorization: Basic s"` with the
      secret as `****`.
- [x] A test there logs the bare carriage return input above with `s` withheld.
- [x] A new ADR-0182 amendment and the rule's doc comment state the behaviour and which limits it closes.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the library stays at 100% line and branch coverage.

## Notes

- Decided (ADR-0182 amendment 33): blanks before a header name are trimmed; a header is read at the line's start and
  after every bare CR, and the first withheld one is masked to the end of the line. A scheme word is kept only when its
  credentials follow it before the next CR. Names are matched within one CR-separated piece, so a hostile 64 KiB line
  costs linear time.
- The review found `a AUTH PLAIN x<CR>X-Api-Key: s` still showing `s`: the SASL rule masked earlier and kept the tail.
  Fixed by adding CR to `SaslLineRedactionRule`'s argument splitters (inside `touches`), so the SMTP, IMAP and POP3
  logs also write nothing after the first mask on such a line. Recorded in amendment 33, not in ADR-0183.
- `PASS`/`ACCT` after a bare CR is left logged, as an accepted limit: filed as BL-1785. Help text is BL-1784's.
- Verify: build `-warnaserror` 0 warnings, format clean, fast tests 30023 passed, 0 failed, 3 skipped.
  `Surl.LineProtocol` and `Surl.Protocol.Socks`: 100% line and branch, 0 failing members.
- A first whole-solution coverage run stopped on a timing test in `Surl.Core.UnitTests` that passed on rerun: BL-1786.
  Coverage was then measured from the two libraries' own test projects.
- No conformance run: curl sends neither line shape, and only the log changes.

## Log

- 2026-11-25: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. The SOCKS and HTTP proxy tunnel logs withhold a secret header's value after leading blanks and after a bare carriage return (ADR-0182 amendment 33).
