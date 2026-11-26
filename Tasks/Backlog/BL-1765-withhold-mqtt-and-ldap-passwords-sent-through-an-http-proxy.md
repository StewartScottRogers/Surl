---
id: BL-1765
title: Withhold MQTT and LDAP passwords sent through an HTTP proxy CONNECT tunnel from the exchange log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1734, BL-1767]
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Protocols/Http/Surl.Protocol.HttpProxy.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed:
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

- [ ] `HttpProxyExchangeLogRedactionTests.ConnectTunnel_MqttConnectPassword_IsAbsentFromTheLog` passes, also when the
      client's bytes arrive one at a time.
- [ ] `HttpProxyExchangeLogRedactionTests.ConnectTunnel_LdapSimpleBindPassword_IsAbsentFromTheLog` passes.
- [ ] The tests the proxy library held before this task pass with their expected output unchanged.
- [ ] A new ADR-0182 amendment, the redactor's doc comment and the proxy library's `CLAUDE.md` state the behaviour.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the proxy library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Now depends on BL-1767 (Stewart: 'prioritise 1767'): fix this in the shared tunnel line rules BL-1767 moves into Surl.LineProtocol, not in a per-server copy.
