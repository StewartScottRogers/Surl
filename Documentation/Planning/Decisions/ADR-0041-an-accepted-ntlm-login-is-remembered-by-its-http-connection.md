# ADR-0041 — An accepted NTLM login is remembered by its HTTP connection

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-133.
- **Amends:** [ADR-0039](ADR-0039-http-ntlm-challenge-and-ntlmv2-check.md), "Consequences".

## Context

NTLM logs in a connection, not a request. ADR-0039 left an accepted login unremembered, so a
later request on the same connection without an `Authorization` was challenged again
([ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) section 4,
step 5).

### Measured

Pinned upstream curl 8.21.0 (win-x64 reference build), `Record-CurlExchange.ps1 -Port 18133
-ResponsesPerConnection 3`, `--ntlm -u tester:secret` with two URLs, `/x` and `/y`, answered with
ADR-0039's recorded `CHALLENGE_MESSAGE` and then two `200`s (fixture `ntlm-two-urls` in
`Surl.Authentication.UnitTests/Fixtures`). curl sent the `NEGOTIATE_MESSAGE` and the
`AUTHENTICATE_MESSAGE` for `/x`, then `GET /y` on the same connection with **no `Authorization`
field**, and exited 0 with `okok`. This is `lib/http_ntlm.c`'s `NTLMSTATE_LAST`: once the
handshake succeeded, the connection is authenticated and curl stops sending the header.

## Decision

1. `HttpAuthenticationSession` - one per connection - remembers the account a
   connection-authenticating method last accepted on it. A request that the session would
   otherwise judge as carrying no credentials (no `Authorization`, or one no accepted method
   takes) proceeds as that account, read or write, with no `WWW-Authenticate` values and no
   `CheckedLogin`: nothing was checked, so no login note is written (ADR-0038).
2. Which methods authenticate the connection is `AuthenticationMethods.AuthenticatesConnection`:
   NTLM only for now. Negotiate carrying NTLM very likely behaves the same in upstream curl, but
   the reference build sent no Negotiate token on the lane machine (ADR-0040, "Measured"), so it
   is not pinned until it is measured.
3. Every later NTLM `Authorization` on the connection starts a new handshake and replaces the
   remembered login: the account when it is accepted, none otherwise. So a restarted handshake
   leaves the connection logged out until it is accepted again, and a refused one keeps it
   logged out.
4. A request carrying another method's `Authorization` is judged by that authorization alone,
   as before; `--allow-anonymous` still proceeds before any of this.
5. The `IHttpCredentialVerifier` seam is unchanged: the verifier reports `Accepted` as before,
   and the session, which already outlives each request and dies with the connection, holds
   the memory. Everything stays inside `Surl.Authentication` (ADR-0032 decision 6).

## Consequences

- Several URLs over one connection with `curl --ntlm` are served, as curl expects.
- The memory dies with the connection, as ADR-0039 wants of the handshake itself; a new
  connection is challenged again.
- Remembering a Negotiate login is later work, once a Negotiate exchange from pinned upstream
  curl has been recorded (BL-134).
