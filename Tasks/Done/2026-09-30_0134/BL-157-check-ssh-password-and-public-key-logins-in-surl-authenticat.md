---
id: BL-157
title: Check SSH password and public-key logins in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-156]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests]
requirement: FR-040
created: 2026-09-29
completed: 2026-09-29
---
# BL-157 — Check SSH password and public-key logins in Surl.Authentication

## Goal

`Surl.Authentication`'s `AuthenticationPolicy` implements BL-156's SSH login contract: it
checks an SSH password against the accounts without refusing it as plain-text, answers
whether a public key is authorized for a user, and parses the authorized-keys file BL-154's
ADR defines.

## Context

- Decisions: BL-154's ADR (the contract, the authorized-keys source and format, the
  `CheckedLogin` method words, `--allow-anonymous` for SSH); ADR-0032 sections 2 and 8 (every
  comparison `CryptographicOperations.FixedTimeEquals` over SHA-256, an unknown user costs the
  same work as a wrong password via the dummy account, a refused credential answered after the
  fixed 1-second delay on the injected `TimeProvider`, no password ever in a note); ADR-0038
  (the login note on the verdict).
- Code: `Surl.Authentication.UnitLibrary/AuthenticationPolicy.cs`, `AccountBook.cs`,
  `CryptographicSecretComparer.cs`, `UserFileParser.cs` (the model for a file parser with
  line-numbered refusals). A public key is compared as the exact key blob (RFC 4253 section
  6.6) with `FixedTimeEquals`.
- Reading the file from disk is `Surl.Console`'s (BL-171); this library parses its bytes, as
  `UserFileParser` does, and returns typed refusals `Surl.Console` turns into the ADR's exit
  code and text.

## Acceptance criteria

- [x] Tests cover: a matching SSH password accepted over a connection with no `TlsSession`; a
      wrong password, an unknown user and no accounts each refused with the same verdict and
      the 1-second delay (on a fake `TimeProvider`); an authorized key accepted for its user and
      refused for another user; `--allow-anonymous` as the ADR decides; each authorized-keys
      refusal the ADR lists, with its line number.
- [x] No test, note or refusal text holds a password or private key.
- [x] `dotnet build Surl.Authentication.UnitLibrary -warnaserror` is clean; the fast tests pass;
      `Measure-CodeQuality.ps1 -Library Surl.Authentication.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

- Built as ADR-0051 sections 6 and 7 decide; no new ADR - the choices below are details inside it.
- `AuthenticationSettings` gains an `init` property `AuthorizedKeys` (default `AuthorizedKeyBook.Empty`)
  rather than a new positional parameter, so `Surl.Console` (outside `touches`) still compiles until
  BL-171 composes the keys.
- Public keys are compared as SHA-256 of the exact blob with `FixedTimeEquals`, against every key of
  the user (a random dummy for an unknown or non-UTF-8 user), as ADR-0032 section 8 compares passwords.
- A signed request with `InvalidSignature` is refused after the delay even when the key is authorized;
  a query is never delayed or noted (ADR-0051 section 7).
- Parsing: leading spaces and tabs are skipped before the `#` test, as sshd does. Options are recognised
  when the first field is not a supported key type and holds `=`, `"` or `,`, or is one of sshd(8)'s
  flag options (case-insensitive); any other unknown first field is `key type <type> is not supported`.
  Blobs are checked field by field per type (Ed25519 32 bytes; ECDSA curve name and uncompressed
  point length; RSA two, DSA four non-empty mpints; nothing trailing).
- `AuthorizedKeysLineFailure.Describe()` escapes the key type itself (ADR-0006 section 3's rules,
  a few lines) because `Surl.Output`'s `ExchangeLogEscaping` is not referenced by this library.
- No upstream curl measurement: nothing here is on the wire (SSH authenticates after encryption);
  BL-172 proves the logins against pinned curl.
- Measured: 686 tests pass; `Measure-CodeQuality.ps1 -Library Surl.Authentication.UnitLibrary` 100% line,
  100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. AuthenticationPolicy checks SSH none, password, keyboard-interactive and public-key logins, and AuthorizedKeysParser reads authorized_keys files with ADR-0051's line-numbered refusals
