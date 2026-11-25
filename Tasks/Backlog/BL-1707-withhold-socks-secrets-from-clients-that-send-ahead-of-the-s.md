---
id: BL-1707
title: Withhold SOCKS secrets from clients that send ahead of the server's replies
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1706]
touches: [Protocols/Proxy/Surl.Protocol.Socks.UnitLibrary, Protocols/Proxy/Surl.Protocol.Socks.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1707 — Withhold SOCKS secrets from clients that send ahead of the server's replies

## Goal

A client that does not wait for the server's SOCKS replies still has its RFC 1929 password and its tunnelled secrets
logged as `****`.

## Context

- `Socks5PasswordRedactor` starts masking only after it sees the method selection, and `SocksExchangeBytesRedactor`
  starts the tunnel phase only after the success reply (ADR-0182 Amendment 6). A client that sends the login with its
  greeting, or tunnelled bytes with its request, has them logged raw. curl waits, so curl is not affected.
- The server forwards bytes sent after the request to the origin (`SocksClientReader`), so such a client works today.
- Following the request's own length fields finds where the tunnel starts without waiting for the reply.

## Acceptance criteria

- [ ] A named fast test sends greeting and RFC 1929 login in one read, before the method selection, and finds the
      password nowhere in the logged output.
- [ ] A named test sends a SOCKS5 request followed by `PASS s3cret-pw\r\n` in one read and logs `PASS ****\r\n`.
- [ ] A named test does the same for a SOCKS4 and a SOCKS4a request.
- [ ] ADR-0182 gains an amendment and Amendment 6's limit refers to it.
- [ ] Build with `-warnaserror` is clean, fast tests are green, and the SOCKS library stays at 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-25: Created.
