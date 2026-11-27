---
id: BL-1846
title: Correct the claim that an account snapshot holds no clear password in the authentication docs, glossary and ADRs
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1829]
touches: [Foundation/Surl.Authentication.UnitLibrary, Documentation/Wiki/Glossary.md, Documentation/Planning/Decisions/ADR-0036-digest-nonces-stale-replay-and-user-name-bytes.md, Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md, Documentation/Planning/Decisions/ADR-0132-how-the-account-book-changes-while-the-server-runs.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1846 — Correct the claim that an account snapshot holds no clear password in the authentication docs, glossary and ADRs

## Goal

The authentication library's docs, the glossary and the ADRs say that an account snapshot of a named account holds
the password's UTF-8 bytes, instead of saying it holds no clear password.

## Context

Found while running BL-1831 (checked against the code on 2026-11-27). Prose and XML doc comments only; no
executable line changes, and what is stored does not change (Signature Version 4 and the challenge-response logins
need the bytes). No store, event or fault changes.

The facts, in `Foundation/Surl.Authentication.UnitLibrary`:

- `AccountStore.DeriveNamed` puts `Encoding.UTF8.GetBytes(account.Password)` into `AwsSigV4Account.Secret` and
  `ChallengeResponseAccount.Password`.
- `AccountStore.TakeSnapshot` copies the `DerivedAccount`s, so a snapshot holds those bytes for every named
  account. The empty-name (Bearer token) account has no named credentials and is held as the SHA-256 only.
- The `Account.Password` string itself is not kept.
- The proxy accounts are an account book too. TLS-SRP accounts keep verifiers only
  (`Cryptography/Surl.TlsSrp.UnitLibrary/TlsSrpAccounts.cs`), so statements about them are true.
- BL-1831 already says this in the class summary of `Application/Surl.Api/SurlSnapshot.cs`.

Where the claim is made (find each by its text; line numbers move):

- `AccountSnapshot.cs`, class summary: "never a clear password".
- `AccountBook.cs`, `TakeSnapshot` summary: "never a clear password".
- `DerivedAccount.cs`, summary: "The clear password is not kept."
- `CLAUDE.md`, bullet under "Accounts changed at run time": "A snapshot holds hashes and derived credentials,
  never `Account.Password`" (literally true of the string, misleading about its bytes).
- `Documentation/Wiki/Glossary.md`, row "snapshot": "accounts (password hashes only)". BL-1829 rewrites that
  row's store list first; correct the password clause if it is still there.
- ADR-0124 decision 3, "Snapshot" bullet: "accounts as password hashes only, never a clear password".
- ADR-0132 decision 5, headed "Snapshots hold no clear password".
- ADR-0036 decision 7: "still keeps no clear password" (the Signature Version 4 and challenge-response bytes of
  ADR-0043 and ADR-0049 are kept).
- `Documentation/Planning/Decisions/README.md`, row 0132: "never a clear password".

An ADR's decision text is not rewritten: each gets a dated amendment, "Decided by Claude under Stewart's
delegation", and an "Amended by" line in its header, as the earlier amendments of ADR-0124 and ADR-0132 do.
`Application/Surl.Api/README.md` makes the same claim and is BL-1847's.

## Acceptance criteria

- [ ] The summaries of `AccountSnapshot`, `AccountBook.TakeSnapshot` and `DerivedAccount`, and the library
      `CLAUDE.md` bullet, say that a named account is kept with the password's UTF-8 bytes among its derived
      credentials, so a snapshot holds secrets, and that the `Account.Password` string is not kept.
- [ ] No file in `Foundation/Surl.Authentication.UnitLibrary` says a snapshot or a kept account holds no clear
      password.
- [ ] The glossary's "snapshot" row no longer says "password hashes only" and says a named account is copied
      with the password's UTF-8 bytes.
- [ ] ADR-0036, ADR-0124 and ADR-0132 each carry a dated amendment, named in the header, that states which
      sentence is superseded and what the code keeps.
- [ ] The ADR index row for 0132 no longer says a snapshot holds never a clear password.
- [ ] `dotnet build Foundation/Surl.Authentication.UnitTests -warnaserror` is clean, and the only changed lines
      in `.cs` files are comments.

## Notes

## Log

- 2026-11-27: Created.
