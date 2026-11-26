---
id: BL-1780
title: Withhold MQTT and LDAP passwords sent after a PROXY protocol line through a SOCKS tunnel
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1765]
touches: [Protocols/Mail/Surl.LineProtocol.UnitLibrary, Protocols/Mail/Surl.LineProtocol.UnitTests, Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md, Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Tasks/Backlog/BL-1764-update-aihelp-and-the-manual-once-the-open-exchange-log-reda.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
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

- [x] A named fast test shows the MQTT password after a `PROXY` line through a granted SOCKS tunnel logged as `****`,
      also when split across reads at every byte.
- [x] A named fast test shows the same for an LDAP simple bind.
- [x] The SOCKS library's earlier redaction tests pass with their expected output unchanged.
- [x] A new ADR-0182 amendment and the SOCKS `CLAUDE.md` state the behaviour.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the touched libraries stay at 100% line and branch
      coverage.

## Notes

- **Plan.** Move the proxy's `PROXY`-line following into `Surl.LineProtocol` as `TunnelBinaryLoginStartFinder`; the
  SOCKS tunnel redactor and the HTTP proxy both call it. Recorded as ADR-0182 amendment 25.
- **`touches` widened.** `Surl.Protocol.HttpProxy.UnitLibrary`, because the logic moved out of it (the task asked for
  that; its tests pass unchanged), and `BL-1764`, which now depends on this task and covers the out-of-date help
  sentences. No other task in `Doing` named either.
- **Help text left to BL-1764**, as amendment 24 did: `--aihelp` and the manual still say the first tunnelled byte
  chooses the MQTT and LDAP redactors.
- **Measured.** Pinned curl 8.21.0 sent the `PROXY` line then the MQTT `CONNECT` through SOCKS5 (fixture
  `socks5h-haproxy-mqtt-user-and-password`). For `ldap://` it sent only the `PROXY` line and exited 38 (WinLDAP binds
  outside the tunnel), so the LDAP test joins that line to the bind recorded against a direct LDAP server.
- **Tests.** `Mqtt311ConnectAfterAProxyLine_AsCurlSendsIt_IsLoggedWithThePasswordMasked`,
  `SimpleBindAfterAProxyLine_IsLoggedWithThePasswordMasked`, `TunnelBinaryLoginStartFinderTests`. Fast tests: 29895
  passed, 0 failed. `Surl.LineProtocol`, `Surl.Protocol.Socks` and `Surl.Protocol.HttpProxy`: 100% line and branch.
- **Review.** One finding, recorded as an amendment 25 accepted limit: no HTTP/2 preface is looked for after a binary
  login byte that follows a `PROXY` line (crafted clients only). Not fixed, so that both proxies treat that byte alike.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. MQTT and LDAP passwords sent after a PROXY line through a SOCKS tunnel are logged as ****
