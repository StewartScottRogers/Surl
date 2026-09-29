# Surl.Authentication.UnitLibrary

Phase 1.

The server side of every authentication scheme upstream curl can send: issues the
challenge (`WWW-Authenticate`, `Proxy-Authenticate`) and verifies the answer for Basic,
Digest, NTLM, Negotiate (SPNEGO and Kerberos), Bearer and AWS Signature Version 4, and for
the SASL mechanisms curl uses with the mail protocols. Anything time-dependent (nonces,
signature windows) takes an injected `TimeProvider`.

This library references `Surl.Protocol.Abstractions.UnitLibrary` and no protocol server.
Protocol servers receive what it provides through the contracts in Abstractions.

## What is here now (BL-110)

- `Account`, `AccountBook`: the configured accounts. `AccountBook` keeps each password as
  the SHA-256 of its UTF-8 bytes and checks a password or Bearer token by comparing SHA-256
  hashes through `ISecretComparer` (`CryptographicSecretComparer`, i.e.
  `CryptographicOperations.FixedTimeEquals`); an unknown user is compared against a random
  dummy hash, so it costs the same and answers the same (ADR-0032 section 8). The empty
  user name is the Bearer token's account and never matches `CheckPassword`.
- `UserFileParser`: the `--user-file` text (bytes, never the disk) into accounts, after the
  `--user` ones, or the first refused line as a `UserFileLineFailure` whose `Describe()` is
  ADR-0032 section 2's text after the path.
- `AuthenticationMethod`, `AuthenticationMethods`: section 3's methods in its order, the
  default set, which send a plain-text secret, and the `Authorization` scheme of each.
- `AuthenticationPolicy` (with `AuthenticationSettings`) implements `IAuthenticationPolicy`:
  section 5 for password logins, section 4 for HTTP through `HttpAuthenticationSession`, and
  the 1-second refusal delay on the injected `TimeProvider`.
- `IHttpAuthenticationMethod` / `IHttpCredentialVerifier` / `HttpCredentialCheck`: the seam
  each HTTP method (BL-111, BL-113, BL-120, BL-121, BL-122) implements. The policy offers and
  checks only the methods it is given that `--auth` accepts; an `Authorization` of any other
  method is treated as missing.

## Basic and Bearer (BL-111)

- `BasicAuthenticationMethod` offers `Basic realm="surl", charset="UTF-8"` and checks the
  base64 `user-id:password`: split at the first `:`, the user-id read as UTF-8 and the
  password compared as the bytes sent (ADR-0035). Not base64, no `:`, or a user-id that is
  not UTF-8 is a refusal, never an exception.
- `BearerAuthenticationMethod` offers `Bearer realm="surl"` and checks the token, turned back
  into the bytes sent with Latin-1, against the empty-name account; an accepted token's
  `AccountName` is the empty string, and an empty token is refused.
- Both hold no per-connection state: `StartConnection` returns the method itself. The
  `Authorization` values they are tested with are recorded from pinned upstream curl in
  `Surl.Authentication.UnitTests/Fixtures` (see its README).

## Digest (BL-113)

- `DigestAuthenticationMethod` offers ADR-0032 section 4's three challenges (MD5, SHA-256,
  SHA-512-256, one nonce) and checks `qop=auth` answers under them or their `-sess` forms;
  `DigestAnswer` and `DigestParameterParser` read the answer, `DigestAlgorithmName` the
  `algorithm`, and `DigestCalculation` is RFC 7616 section 3.4's arithmetic (SHA-512/256 from
  `Surl.Cryptography`, which this library references for it, ADR-0032 section 7).
- `DigestNonceBook` (behind `IDigestNonceBook`) issues ADR-0036's signed nonces on the injected
  `TimeProvider`, expires them after five minutes, and remembers each used nonce's `nc` so a
  replayed answer is refused. A right answer on an expired nonce is a `Continue` carrying the
  challenges with `stale=true`, so the policy answers it undelayed.
- `AccountBook.FindDigestAccount` holds each named account's user hashes, computed at
  start-up, under its UTF-8 spelling and, for an all-ISO-8859-1 account, its ISO-8859-1 one;
  an unknown name gets a random dummy.
