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
