---
id: BL-1765
title: Withhold MQTT and LDAP passwords sent through an HTTP proxy CONNECT tunnel from the exchange log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1734, BL-1767]
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests, Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed: 2026-11-26
---
# BL-1765 — Withhold MQTT and LDAP passwords sent through an HTTP proxy CONNECT tunnel from the exchange log

## Goal

An MQTT `CONNECT` password and an LDAP simple bind password sent through an HTTP proxy `CONNECT` tunnel are logged as
`****`.

## Context

- Found while working BL-1734. ADR-0182 amendment 16 masks text logins in the tunnel; a tunnel whose first byte is
  `0x10` or `0x30` still goes to `HttpRequestHeadBytesRedactor`, which masks only HTTP credential fields.
- The SOCKS tunnel is the model: `SocksTunnelMqttConnectRedactor` and `SocksTunnelLdapBindRedactor` (amendment 10).
  Protocol libraries never reference each other. Writing the proxy's own parsers and moving the SOCKS ones into a new
  shared library are both open; a new library needs an ADR and changes this task's `touches`.
- Only the levelled log and trace dumps change; the event stream keeps raw bytes (ADR-0182 decision 4).

## Acceptance criteria

- [x] `HttpProxyExchangeLogRedactionTests.ConnectTunnel_MqttConnectPassword_IsAbsentFromTheLog` passes, also when the
      client's bytes arrive one at a time.
- [x] `HttpProxyExchangeLogRedactionTests.ConnectTunnel_LdapSimpleBindPassword_IsAbsentFromTheLog` passes.
- [x] The tests the proxy library held before this task pass with their expected output unchanged.
- [x] A new ADR-0182 amendment, the redactor's doc comment and the proxy library's `CLAUDE.md` state the behaviour.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the proxy library stays at 100% line and branch
      coverage.

## Notes

- Decision (ADR-0182 amendment 23): the SOCKS MQTT and LDAP redactors moved into `Surl.LineProtocol`, as this task's
  2026-11-25 Log line asks, instead of a copy in the proxy. That changes the SOCKS library and its tests, so both were
  added to `touches`; the board in this checkout had no other task in `Doing`.
- Detailed MQTT, LDAP and BER tests moved to `Surl.LineProtocol.UnitTests`; the SOCKS tests keep the curl-recorded
  cases through a granted tunnel.
- Measured pinned curl 8.21.0 with `-p -x ... mqtt://`, with and without `--haproxy-protocol` (two new proxy
  fixtures). With it, a `PROXY` line comes before the MQTT `CONNECT`, so the proxy judges the byte after that line too.
- The proxy finds a `CONNECT` head's end with its own `ConnectHeadEndFinder`: review showed the redactor's existing
  state missed it after a leading line feed, after a body in the same read, and on a retry after `Content-Length: 0`.
  A second review found a 64 KiB line cap that hid the head, so the finder keeps no line.
- Accepted limits (amendment 23): a `0x30` tunnel that used to be read as text (an IMAP tag beginning `0` after a
  `PROXY` line or an unusual head) is now read as LDAP, as in SOCKS; crafted client bytes that look like a `CONNECT`
  head can switch the rest of a connection to a binary redactor.
- Pipeline: planned, tested and implemented in the lane's own session, then two `code-reviewer` passes. Conformance
  auditor skipped: only the exchange log changes, no byte Surl sends or accepts.
- Results: fast tests 29,842 passed, 0 failed; the three libraries at 100% line and branch coverage.
- Follow-up: BL-1779 (the same `PROXY` line gap through SOCKS). BL-1744's context updated to the new class name.
- Seen, not changed: two Done task files carry ID BL-1773 (the board script warns).

## Log

- 2026-11-25: Created.
- 2026-11-25: Now depends on BL-1767 (Stewart: 'prioritise 1767'): fix this in the shared tunnel line rules BL-1767 moves into Surl.LineProtocol, not in a per-server copy.
- 2026-11-25: Backlog -> Doing.
- 2026-11-26: Doing -> Done. MQTT CONNECT and LDAP bind passwords sent through an HTTP proxy CONNECT tunnel, also after a PROXY line, are logged as ****
