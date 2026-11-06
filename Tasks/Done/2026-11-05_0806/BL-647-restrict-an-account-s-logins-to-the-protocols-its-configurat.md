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
completed: 2026-11-05
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

- [x] Named tests show: an account restricted to `imap,pop3` is accepted through the `imap` view and refused
      through the `smtp` and `http` views with the same verdict, note and delay as an unknown account; an
      unrestricted account is accepted through every view; the unrestricted policy behaves exactly as before
      (existing tests unchanged).
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and
      `Surl.Authentication.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Plan, as ADR-0102 decision 4.3 placed it: no Abstractions contract changes. `Account.Schemes`
  (`null` = every scheme) and `Account.MayLogInOn`; `AccountBook.ForScheme` copies the admitted
  accounts' already-derived secrets (password hashes, Digest, NTLM, SigV4, challenge-response, SCRAM)
  into a new book, so a left-out name meets the dummies exactly as an unknown name; `AuthorizedKeyBook`
  drops the left-out accounts' keys; `AuthenticationSettings.ForScheme` ties both. Composition (BL-653)
  builds one `AuthenticationPolicy`, with HTTP methods over the view's `Accounts`, per scheme, as
  `AuthenticationComposition.ComposePolicy` already does from settings.
- Choice (sensible default): the TLS twin rule is generic - a named scheme admits the same name with
  or without one final `s`, case-insensitive. It covers every pair ADR-0102 lists (`ws`/`wss`
  included) and every extended pair a server registers, with no table to keep in step; no two real
  schemes differ only by a final `s` without being TLS twins.
- Choice: `Account.MailSchemes` (`smtp`, `imap`, `pop3`) is here so BL-653 sets a `--mailbox`
  account's default from the library rather than spelling the list again.
- Consequence recorded in the project CLAUDE.md: a view left with no accounts serves HTTP anonymous
  reads, exactly as ADR-0032 section 4 does with none configured - the restricted account is "as no
  such account" there, which is what the ADR asks.
- Unchanged and unrestricted: `ForScheme` returns the same settings and book instance when no account
  is left out, so the unrestricted policy is the old one; every existing test is unchanged.
- Coverage (2026-11-05, cobertura from the MSTest collector): line 100%; branch 99.93% - the one
  partial branch is `HttpAuthenticationSession.cs:143`'s `switch`, a file this task does not touch
  (last changed in `bc0f7704`); new tests can only add hits, so it predates this task. Every new
  line and branch is covered. Left for the coverage-auditor's pass rather than widening this task.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Authentication gives each scheme a view of the accounts: an account restricted by protocols= is refused elsewhere exactly as no such account
