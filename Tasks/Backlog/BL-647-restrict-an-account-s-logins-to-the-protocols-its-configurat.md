---
id: BL-647
title: Restrict an account's logins to the protocols its configuration names
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-642]
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-647 — Restrict an account's logins to the protocols its configuration names

## Goal

`Surl.Authentication` can give each server a view of the account set in which an account restricted to some
protocols (`--mailbox …;protocols=imap,pop3`) exists only for those schemes, so a login on any other scheme is
refused exactly as for no such account.

## Context

- Decision: BL-642's ADR decision 4 (how `protocols=` is enforced). Planning assumption: a per-scheme view of
  `AuthenticationPolicy`/`AccountBook` that the composition hands each server, so no contract in
  `Foundation/Surl.Protocol.Abstractions.UnitLibrary` changes; if the ADR puts it elsewhere and this task would
  have to touch Abstractions, stop, record that in Notes, and have the board re-plan it (touching Abstractions
  serialises with every protocol task).
- Where: `Foundation/Surl.Authentication.UnitLibrary` (`Account`, `AccountBook`, `AuthenticationPolicy`,
  `AuthenticationSettings`). ADR-0032 section 8: a refused login is indistinguishable from no such user and waits
  the same one-second delay on the injected `TimeProvider`; this must hold for a scheme-restricted account too.
- Every login method is covered (password methods, SASL exchanges, Digest, NTLM, Kerberos's account check, SSH
  public keys from `--authorized-keys`): the view filters the accounts, not each method.
- Composition wiring is the mail option task's (BL-653); this task builds the library side.

## Acceptance criteria

- [ ] Named tests show: an account restricted to `imap,pop3` is accepted through the `imap` view and refused
      through the `smtp` and `http` views with the same verdict, note and delay as an unknown account; an
      unrestricted account is accepted through every view; the unrestricted policy behaves exactly as before
      (existing tests unchanged).
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and
      `Surl.Authentication.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
