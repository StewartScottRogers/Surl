---
id: BL-1780
title: Withhold MQTT and LDAP passwords sent after a PROXY protocol line through a SOCKS tunnel
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1765]
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-26
completed:
---
# BL-1780 — Withhold MQTT and LDAP passwords sent after a PROXY protocol line through a SOCKS tunnel

## Goal

An MQTT `CONNECT` password and an LDAP bind sent through a SOCKS tunnel after a PROXY protocol line are logged as
`****`.

## Context

- Found while working BL-1765 (ADR-0182 amendment 23, accepted limits). `SocksTunnelBytesRedactor` reads a tunnel
  whose first byte is `P` as text, so the binary login after curl's `--haproxy-protocol` line is not masked (read from
  the code).
- Pinned upstream curl 8.21.0 sends that line in a tunnel before the MQTT `CONNECT`; recorded through an HTTP proxy
  as `Surl.Protocol.HttpProxy.UnitTests/Fixtures/proxytunnel-haproxy-mqtt-user-and-password`. Record the SOCKS form
  with `Record-CurlExchange.ps1` first.
- The proxy's handling is in `HttpProxyRequestOrTunnelBytesRedactor.FollowTunnelStart`. Move it into
  `Surl.LineProtocol` if both servers can share it; that adds the proxy projects to `touches`.

## Acceptance criteria

- [ ] A named fast test shows the MQTT password after a `PROXY` line through a granted SOCKS tunnel logged as `****`,
      also when split across reads at every byte.
- [ ] A named fast test shows the same for an LDAP simple bind.
- [ ] The SOCKS library's earlier redaction tests pass with their expected output unchanged.
- [ ] A new ADR-0182 amendment and the SOCKS `CLAUDE.md` state the behaviour.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the touched libraries stay at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-26: Created.
