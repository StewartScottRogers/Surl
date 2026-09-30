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
completed:
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

- [ ] Tests cover: a matching SSH password accepted over a connection with no `TlsSession`; a
      wrong password, an unknown user and no accounts each refused with the same verdict and
      the 1-second delay (on a fake `TimeProvider`); an authorized key accepted for its user and
      refused for another user; `--allow-anonymous` as the ADR decides; each authorized-keys
      refusal the ADR lists, with its line number.
- [ ] No test, note or refusal text holds a password or private key.
- [ ] `dotnet build Surl.Authentication.UnitLibrary -warnaserror` is clean; the fast tests pass;
      `Measure-CodeQuality.ps1 -Library Surl.Authentication.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
