---
id: BL-110
title: Keep accounts and apply the anonymous, plaintext and method policy in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-109]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests]
requirement: FR-014
created: 2026-09-29
completed: 2026-09-29
---
# BL-110 — Keep accounts and apply the anonymous, plaintext and method policy in Surl.Authentication

## Goal

`Surl.Authentication` keeps the configured accounts - read from `--user` values and from
`--user-file` text - checks a user name and password in constant time, and applies
ADR-0032's policy (refuse every login with no accounts, anonymous access only where allowed,
plain-text secrets refused on an unencrypted connection unless allowed, only the accepted
methods), implementing BL-109's contract.

## Context

FR-014 and ADR-0032's new rows; ADR-0032 (BL-100) decisions 2 (the `--user-file` format and
its refusals), 3 (methods and which are plain-text secrets), 4 and 5 (HTTP and MQTT policy)
and 8 (constant-time checks, nothing learnable, any delay via `TimeProvider`).

- `Surl.Authentication.UnitLibrary` is empty today (only `CLAUDE.md` and a csproj
  referencing `Surl.Protocol.Abstractions.UnitLibrary`); `Surl.Authentication.UnitTests`
  likewise. `Surl.Console` already references `Surl.Authentication`.
- This library does not touch the disk: it parses the `--user-file` *text* it is given
  (`Surl.Console` reads the file in BL-117), and returns a parse failure with the line
  number and ADR-0032's reason rather than throwing for bad input.
- Password comparison uses `System.Security.Cryptography.CryptographicOperations.FixedTimeEquals`
  over UTF-8 bytes (or over the hash ADR-0032 decision 2 names); a missing user costs the
  same comparison as a wrong password.
- The per-method verifiers are BL-111 (Basic, Bearer), BL-113 (Digest), BL-120 (NTLM),
  BL-121 (Negotiate), BL-122 (SigV4); this task builds what they share (account lookup,
  policy, the accepted-method set) and the contract entry points BL-109 defined, with the
  method list empty until they land.

## Acceptance criteria

- [x] Tests in `Surl.Authentication.UnitTests` prove: with no accounts every login is
      refused; a right password is accepted and a wrong one, an unknown user and an empty
      password are refused; and the policy answers for anonymous access and for a
      plain-text secret on an encrypted and an unencrypted connection, with and without
      each loosening flag, match ADR-0032 decisions 3 to 5 case by case.
- [x] Tests prove the `--user-file` parser accepts ADR-0032's format (including comments,
      blank lines and a password containing `:`) and refuses each malformed form with the
      line number and ADR-0032's text; `--user` accounts and file accounts combine as
      ADR-0032 decision 1 says.
- [x] A test proves the password check calls `CryptographicOperations.FixedTimeEquals`
      (or ADR-0032's hash check) for both an unknown user and a wrong password (e.g. via
      equal-length comparisons counted through a seam), and no exception text or result
      reveals whether a user exists.
- [x] `dotnet build Surl.Authentication.UnitLibrary -warnaserror` is clean; the fast tests
      pass; 100% line and branch coverage; no method exceeds complexity 10.

## Notes

- Delivered in the session without a separate protocol-architect plan: ADR-0032 sections 2,
  3, 4, 5 and 8 already fix every behaviour, and the work is one library and its tests.
- **What was built**: `Account`, `AccountBook` (SHA-256 of each password, compared through the
  internal `ISecretComparer` seam whose production instance is
  `CryptographicOperations.FixedTimeEquals`; an unknown user is compared against a random
  dummy hash), `UserFileParser` (bytes in, `UserFileParseResult` out; `UserFileLineFailure.Describe()`
  is ADR-0032 section 2's text after `User file <path>, `), `AuthenticationMethod` /
  `AuthenticationMethods`, `AuthenticationSettings`, and `AuthenticationPolicy`, which
  implements BL-109's `IAuthenticationPolicy` with the 1-second refusal delay waited on the
  injected `TimeProvider`.
- **Choices taken (sensible defaults, within ADR-0032):**
  - HTTP methods plug in through `IHttpAuthenticationMethod` (challenge values, and
    `StartConnection()` giving a per-connection `IHttpCredentialVerifier`), returning an
    `HttpCredentialCheck` of `Accepted`, `Continue` or `Refused`. The policy owns what they
    share - who needs a login, the unchecked `403` for Basic or Bearer without TLS, section 3's
    order, which challenges are offered on the connection, and the refusal delay - so BL-111 to
    BL-122 only verify. `Surl.Console` (BL-117) passes the implemented methods.
  - An `Authorization` whose method is accepted but not implemented yet is treated as missing
    (step 4), as is any unknown scheme; the method list is empty until BL-111 lands.
  - Only the first `Authorization` field is read; the field name and scheme match
    case-insensitively; the credentials are what follows the scheme and its spaces.
  - Signature Version 4's scheme is `AWS4-HMAC-SHA256`, the one curl's `--aws-sigv4` sends for
    the `aws` provider; other providers' schemes are BL-122's to add.
  - `--user` accounts arrive already parsed (BL-108 in `Surl.Cli`); `UserFileParser.Parse`
    takes them so a file name clashing with a `--user` name is refused with the file's line
    number, and returns them first, then the file's.
  - A refused MQTT-style login is delayed only when credentials were checked
    (`RefusedCredentials`); `RefusedPlaintext` and `RefusedAnonymous` answer at once, as
    section 8 exempts missing credentials and nothing is checked for plain text.
  - An MQTT-style login with an empty (not absent) user name is a user name, so it is checked
    and refused as `RefusedCredentials` (`CONNACK` 4): the empty name is the Bearer token's
    account and never matches a password login (ADR-0032 section 1).
  - After code review: two methods verifying the same `AuthenticationMethod` are refused when
    the policy is built (not on the first connection); a `Continue` check with no value is
    answered as a refusal, since a `401` must carry a challenge; `DefaultAccepted` is a
    `FrozenSet`.
- **Found**: section 8's `Login accepted` / `Login refused` log notes need the method and the
  user as sent, which the section 6 contract does not carry; filed as BL-125.
- **Gotcha** for BL-115 and anyone building a `PasswordLogin`: `cond ? null : someByteArray`
  typed as `ReadOnlyMemory<byte>?` is *not* null - `null` converts to `byte[]` and then to a
  default `ReadOnlyMemory<byte>`, so `Password.HasValue` is true. Cast: `(ReadOnlyMemory<byte>?)null`.
- Gates: 120 tests in `Surl.Authentication.UnitTests`; `Measure-CodeQuality.ps1 -Library
  Surl.Authentication.UnitLibrary` reports 100% line, 100% branch, 0 failing members;
  `dotnet build -warnaserror` and `dotnet format --verify-no-changes` clean.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Surl.Authentication keeps --user and --user-file accounts, checks passwords in constant time and applies ADR-0032's anonymous, plain-text and method policy through IAuthenticationPolicy
