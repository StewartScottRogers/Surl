---
id: BL-1767
title: Move the tunnel line redaction rules the SOCKS server and the HTTP proxy each hold into a shared library
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1734]
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed:
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

- [ ] The shared library holds the lines redactor and the three rules, public, with unit tests in its `.UnitTests`.
- [ ] Neither the SOCKS nor the HTTP proxy library holds a copy of them.
- [ ] The existing SOCKS and HTTP proxy redaction tests pass with their expected output unchanged.
- [ ] A tab-separated `LOGIN` line through a SOCKS tunnel is logged with its password as `****`, pinned by a test.
- [ ] A new ADR-0182 amendment and the three libraries' `CLAUDE.md` files say where the rules are.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the three libraries stay at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Priority Low -> High and made the tunnel redaction follow-ups depend on it (Stewart: 'prioritise 1767').
