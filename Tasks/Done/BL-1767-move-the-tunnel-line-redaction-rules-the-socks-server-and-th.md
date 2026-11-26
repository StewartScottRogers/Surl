---
id: BL-1767
title: Move the tunnel line redaction rules the SOCKS server and the HTTP proxy each hold into a shared library
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1734]
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md, Foundation/Surl.Protocol.Abstractions.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1767 — Move the tunnel line redaction rules the SOCKS server and the HTTP proxy each hold into a shared library

## Goal

One copy of the tunnel line redaction rules, in a library both the SOCKS server and the HTTP proxy reference.

## Context

- BL-1734 (ADR-0182 amendment 16) wrote the HTTP proxy's `HttpProxyTunnelLinesRedactor`,
  `HttpProxyTunnelLineRedactionRule`, `HttpProxyTunnelPasswordAndCredentialHeaderLineRule` and
  `HttpProxyTunnelImapLoginLineRule` as near copies of the SOCKS library's `SocksTunnel*` classes, because protocol
  libraries never reference each other.
- `Surl.LineProtocol` (ADR-0050) is a horizontal library both already reference. The credential header names live in
  `HttpWithheldHeaderNames`; if the shared rule cannot reach them from there, decide where it belongs and say why in
  the amendment.
- The two copies differ in one way: the proxy's `LOGIN` rule separates words with spaces and tabs, the SOCKS one with
  spaces only. The shared rule uses spaces and tabs.

## Acceptance criteria

- [x] The shared library holds the lines redactor and the three rules, public, with unit tests in its `.UnitTests`.
- [x] Neither the SOCKS nor the HTTP proxy library holds a copy of them.
- [x] The existing SOCKS and HTTP proxy redaction tests pass with their expected output unchanged.
- [x] A tab-separated `LOGIN` line through a SOCKS tunnel is logged with its password as `****`, pinned by a test.
- [x] A new ADR-0182 amendment and the three libraries' `CLAUDE.md` files say where the rules are.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the three libraries stay at 100% line and branch
      coverage.

## Notes

- The classes are `Surl.LineProtocol`'s `TunnelLinesRedactor`, `TunnelLineRedactionRule`,
  `TunnelPasswordAndCredentialHeaderLineRule` and `TunnelImapLoginLineRule` (ADR-0182 amendment 20).
  `HttpWithheldHeaderNames` is reachable from there, so no new library or reference was needed.
- Review found that splitting `LOGIN` words on spaces and tabs alone would have shown passwords the SOCKS copy masked
  (a leading tab, a tab inside the tag, a tab as the user name). The shared rule therefore reads each line both ways
  (with tabs, and with spaces only) and masks from the earlier password; the proxy's log masks more for those shapes.
- Existing redaction tests keep their expected output. One edit: `SocksExchangeLogRedactionTests.NullOutput_InATunnel_IsRefused`
  lost the assertion on the removed SOCKS rule class; `TunnelLineRedactionRuleTests.Redact_NullOutput_IsRefused` holds it.
- Pipeline: planned, tested and implemented in the lane's own session (a move of existing code), then `code-reviewer`.
  Conformance skipped: only the exchange log changes, no byte Surl sends or accepts.
- Added `Foundation/Surl.Protocol.Abstractions.UnitLibrary/CLAUDE.md` to `touches`: its sentence on "the SOCKS
  tunnel's line rule" became stale. No other task in this checkout's `Doing` names it.
- Updated the context of BL-1750, BL-1753 and BL-1772 to the new class names; BL-1772 also gained the
  `Surl.LineProtocol` projects in `touches` and BL-1767 in `depends-on`.

## Log

- 2026-11-25: Created.
- 2026-11-25: Priority Low -> High and made the tunnel redaction follow-ups depend on it (Stewart: 'prioritise 1767').
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. The SOCKS server and the HTTP proxy share one copy of the tunnel line redaction rules in Surl.LineProtocol; a tab-separated LOGIN through SOCKS is masked
