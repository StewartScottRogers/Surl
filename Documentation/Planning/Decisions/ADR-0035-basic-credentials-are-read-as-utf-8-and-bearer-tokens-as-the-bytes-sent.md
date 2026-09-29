# ADR-0035 — Basic credentials are read as UTF-8, Bearer tokens as the bytes sent

- **Status:** Accepted
- **Date:** 2026-09-29
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-29,
  in BL-111.

## Context

[ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) fixes the
Basic challenge as `Basic realm="surl", charset="UTF-8"` and keeps every account password as
the SHA-256 of its UTF-8 bytes (section 8), but does not say how the bytes inside a Basic
`Authorization` value are read when they are not ASCII, nor what a Bearer token's bytes are.

Measured on 2026-09-29 with `Record-CurlExchange.ps1` and the pinned reference build (curl
8.21.0, win-x64, SHA-256 `0E773709…8778`; fixtures in `Surl.Authentication.UnitTests/Fixtures`):

- `--basic -u tëster:sé:cr€t` on the Windows command line sent
  `Basic dOtzdGVyOnPpOmNygHQ=`, the Windows-1252 bytes `74 EB 73 74 65 72 3A 73 E9 3A 63 72 80 74`:
  the build receives its arguments in the ANSI code page and sends them unchanged, whatever
  charset the challenge announced.
- The same `user` given in a UTF-8 config file (`-K`) sent `Basic dMOrc3Rlcjpzw6k6Y3Ligqx0`,
  the UTF-8 bytes. A command line on Linux and macOS is UTF-8 as well.
- A password holding `:` is sent as it is; only the first `:` separates the user name
  (RFC 7617 section 2).

## Decision

1. **Basic.** The value after `Basic` is decoded with `Convert.TryFromBase64String`; a value
   that is not base64 is refused. The decoded bytes are split at the first `:` byte; none is a
   refusal. The user-id is read as UTF-8 - the charset Surl announces (RFC 7617 section 2.1) -
   and a user-id that is not UTF-8 matches no account but still costs one password comparison
   (ADR-0032 section 8). The password is compared as the bytes sent. So an account whose name
   or password is not ASCII logs in from a client that sends UTF-8 (Linux, macOS, a config file
   on Windows), and is refused from the Windows reference build's command line.
2. **No second charset is tried.** Falling back to ISO-8859-1 would not match the Windows-1252
   bytes measured (`€` is `0x80` there, a control character in ISO-8859-1), guessing the
   client's code page cannot be done from the request, and trying two readings would give two
   passwords per account. One reading, the announced one, is the simplest faithful answer.
3. **Bearer.** The HTTP server reads every field value one byte per character (Latin-1), so the
   token is turned back into the bytes sent with `Encoding.Latin1` and compared with the
   empty-name account's password (ADR-0032 section 1); a token upstream curl sends from a UTF-8
   argument matches the same token configured by `--user :<token>`. An empty token is refused.
   An accepted token logs in as the empty-name account, so `HttpCredentialCheck.AccountName` is
   the empty string.
4. **Both methods hold no per-connection state**: `StartConnection` returns the method itself.

## Alternatives considered

- **Refuse any non-ASCII credential.** Simpler, but refuses logins a UTF-8 client makes
  correctly; rejected.
- **Decode as the Windows ANSI code page when not UTF-8.** Surl runs on Linux and macOS too,
  where that code page means nothing, and the server cannot know the client's; rejected.

## Consequences

- A Windows user whose account name or password is not ASCII must give it to the reference
  build through a UTF-8 config file (`-K`, measured above) rather than on the command line. This is how
  upstream curl behaves against any UTF-8 server, not a Surl limit.
- `BasicAuthenticationMethod` and `BearerAuthenticationMethod` in `Surl.Authentication` carry
  this decision; `Surl.Console` composes them into the policy (BL-117).
