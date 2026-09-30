# ADR-0044 — An accepted Negotiate login is remembered by its HTTP connection

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-135.
- **Amends:** [ADR-0041](ADR-0041-an-accepted-ntlm-login-is-remembered-by-its-http-connection.md),
  decision 2.

## Context

ADR-0041 remembers an accepted NTLM login for the connection's later requests that carry no
`Authorization`, and left Negotiate out until a Negotiate exchange from pinned upstream curl had
been recorded. BL-134 recorded one with the unpatched 8.21.0 Windows build
([ADR-0042](ADR-0042-negotiate-is-proved-with-the-unpatched-8-21-0-windows-build.md)), so the
question can now be measured. Upstream curl 8.21.0's `lib/http_negotiate.c` stops sending the
header once the context is done, unless the server sends `Persistent-Auth: false`, which Surl
never sends.

### Measured

Pinned upstream curl 8.21.0, stunnel/static-curl's Windows build
(`C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe`, SHA-256 `589C8E4D…2648`),
`Record-CurlExchange.ps1 -Port 18135 -ResponsesPerConnection 3`,
`--negotiate -u tester:secret` with two URLs, `/x` and `/y`, answered with ADR-0042's recorded
`Negotiate` `CHALLENGE_MESSAGE` and then two `200`s (fixture `negotiate-ntlm-two-urls` in
`Surl.Authentication.UnitTests/Fixtures`). curl sent the `NEGOTIATE_MESSAGE` and the
`AUTHENTICATE_MESSAGE` after `Negotiate` for `/x`, then `GET /y` on the same connection with
**no `Authorization` field**, and exited 0 with `okok` - as it does for NTLM (ADR-0041).

## Decision

1. `AuthenticationMethods.AuthenticatesConnection` is true for Negotiate as well as NTLM, so an
   accepted Negotiate login is remembered by `HttpAuthenticationSession` exactly as ADR-0041
   decisions 1, 3, 4 and 5 lay out for NTLM: later requests without credentials proceed as the
   account with no login note, a new Negotiate handshake replaces the login (logged out until it
   is accepted), and the memory dies with the connection.
2. The rule is the method's, not the mechanism's: whatever Negotiate carries, curl's
   `http_negotiate.c` keeps the connection authenticated once the context completes. Negotiate
   carrying SPNEGO-wrapped NTLM, and Kerberos once Surl serves it (ADR-0032 section 11), are
   remembered the same way; only bare NTLM inside Negotiate has been measured, because it is the
   only form a pinned build sends on the lane machine.
3. Surl sends no `Persistent-Auth` field, so curl's non-persistent mode never applies.

## Consequences

- Several URLs over one connection with `curl --negotiate` are served without a new handshake,
  as curl expects; a new connection is challenged again.
- If Surl ever sends `Persistent-Auth: false`, curl would re-authenticate each request and this
  decision would need revisiting.
