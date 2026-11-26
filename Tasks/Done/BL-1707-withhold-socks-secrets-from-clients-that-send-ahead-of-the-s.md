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
completed: 2026-11-25
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

- [x] A named fast test sends greeting and RFC 1929 login in one read, before the method selection, and finds the
      password nowhere in the logged output.
- [x] A named test sends a SOCKS5 request followed by `PASS s3cret-pw\r\n` in one read and logs `PASS ****\r\n`.
- [x] A named test does the same for a SOCKS4 and a SOCKS4a request.
- [x] ADR-0182 gains an amendment and Amendment 6's limit refers to it.
- [x] Build with `-warnaserror` is clean, fast tests are green, and the SOCKS library stays at 100% line and branch
      coverage.

## Notes

- Tests, all in `SocksSendAheadRedactionTests`: `GreetingAndLoginInOneRead_BeforeTheMethodSelection_LogNoPassword`,
  `Socks5RequestAndPassInOneRead_LogThePasswordMasked`, `Socks4RequestAndPassInOneRead_LogThePasswordMasked`,
  `Socks4aRequestAndPassInOneRead_LogThePasswordMasked`.
- Design (ADR-0182 amendment 12): `SocksHandshakeBytesRedactor` replaces `Socks5PasswordRedactor` and follows the
  client's length fields to the request's last byte. It reads no server reply: the byte after the greeting and the
  listener's keytab say what follows. A first version used the method selection when it had been sent; review showed
  that a login sent in a second read after `05 00` was then logged in clear, and that a GSS-API client offering `01`
  and `02` lost the tunnel start. Three tests that pinned "the selection decides" now expect the password masked.
- The success reply still starts the tunnel when the request's end was not found, and the tunnel redactor is shown
  only what is sent after it.
- The plan, tests and code were written in the run's own session (a one-library change); `code-reviewer` reviewed it.
- Live check, 2026-11-25: pinned curl 8.21.0 `-x socks5h://app:s3cret-pw@... -u web:t0ken-pw` through the built `surl`
  exited 0; the `--trace-ascii` dump showed `..app.****` and `Authorization: Basic ****`.
- Measured: 29437 fast tests passed, 3 skipped; SOCKS library 100% line, 100% branch, highest complexity 9.
- The `--aihelp` paragraph that says an early sender's secrets can be logged is BL-1723's (it depends on this task).

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. A SOCKS client that sends ahead of the server's replies has its RFC 1929 password and tunnelled secrets logged as ****
