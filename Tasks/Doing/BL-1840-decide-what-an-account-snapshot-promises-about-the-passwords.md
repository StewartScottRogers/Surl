---
id: BL-1840
title: Decide what an account snapshot promises about the passwords it holds
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-27
completed:
---
# BL-1840 — Decide what an account snapshot promises about the passwords it holds

## Goal

A decision record states what an account snapshot holds of each password, and matches what `AccountStore` does.

## Context

Found while running BL-1829 (checked against the code on 2026-11-27). Normal priority because the false statement
is a security promise that ships in the XML docs of public types (BL-1841 corrects those).

- ADR-0124 decision 3 says a snapshot holds accounts "as password hashes only, never a clear password".
- `Foundation/Surl.Authentication.UnitLibrary/AccountStore.cs`, `DeriveNamed`, keeps each named account's password
  as its UTF-8 bytes in `AwsSigV4Account.Secret` and `ChallengeResponseAccount.Password`, and keeps the bytes a
  base64 password decodes to in `AzureStorageAccount.Key`. `TakeSnapshot` copies those `DerivedAccount`s as they
  are. The empty-name Bearer-token account has no derived credentials: only its SHA-256.
- The bytes are needed: Signature Version 4 (`SignatureV4Check`) derives each signing key from the secret
  (ADR-0043), `CramMd5SaslExchange` and `DigestMd5SaslExchange` and the other challenge-response logins read
  `FindChallengeResponseAccount` (ADR-0049 section 5), and `AccountBook.TryFindPreSharedKey` serves `coaps`
  (ADR-0170 decision 5). Dropping them would remove logins, so the expected decision is to keep the code and
  correct the promise. Confirm that against the code.
- ADR-0132 decision 5 is headed "Snapshots hold no clear password" and also says a method that needs the secret
  keeps the derived bytes; it says the snapshot never holds `Account.Password` (the string).
  `Documentation/Planning/Decisions/README.md`, ADR-0132 row, repeats "never a clear password". ADR-0036 decision 7
  says `AccountBook` "still keeps no clear password", written before ADR-0043 added the UTF-8 bytes.
- The glossary's "snapshot" row is already corrected by BL-1829.
- Design question delegated to Claude (root `CLAUDE.md`, "Decisions"): record it as an amendment to ADR-0132, with
  pointers, marked "Decided by Claude under Stewart's delegation". Weigh at least: what a snapshot (and the running
  book) holds, that it is in memory, opaque and exposes only `UserNames`, and that no event or log line carries it.
- Stand-in: documents only. No store, event or fault changes.
- No upstream curl behaviour is involved.

## Acceptance criteria

- [ ] ADR-0132 has an amendment, marked "Decided by Claude under Stewart's delegation", saying that a snapshot of a
      named account holds the password's SHA-256 and derived credentials, that some of those are the password's
      UTF-8 bytes (or, for Azure Storage, the bytes a base64 password decodes to), which login methods need them,
      and why they are kept; the ADR's "Amended by" line lists it.
- [ ] The amendment says the empty-name Bearer-token account is held as its SHA-256 only.
- [ ] ADR-0124 decision 3 no longer states "password hashes only, never a clear password" without a correction or
      a pointer to the amendment, and its "Amended by" line names it.
- [ ] ADR-0036 decision 7's "still keeps no clear password" carries a pointer to the amendment or to ADR-0043.
- [ ] The ADR index rows in `Documentation/Planning/Decisions/README.md` for ADR-0132 and ADR-0124 state nothing
      the amendment contradicts.
- [ ] Only files under `Documentation/Planning/Decisions` change.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
