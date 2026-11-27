---
id: BL-1837
title: Say in the account snapshot docs that some derived credentials are the password's bytes
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1836, BL-1831]
touches: [Foundation/Surl.Authentication.UnitLibrary, Application/Surl.Api/SurlSnapshot.cs, Application/Surl.Api/README.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1837 — Say in the account snapshot docs that some derived credentials are the password's bytes

## Goal

The library and `Surl.Api` documents about accounts and their snapshots say what is held of each password, as the
amendment BL-1836 records.

## Context

Found while running BL-1829 (checked against the code on 2026-11-27). Prose and XML doc comments only; no
executable line changes. No store, event or fault changes. Read the ADR-0132 amendment BL-1836 adds first.

`AccountStore.DeriveNamed` keeps each named account's password as UTF-8 bytes (`AwsSigV4Account`,
`ChallengeResponseAccount`) and a base64 password's decoded bytes (`AzureStorageAccount`), and a snapshot copies
them. These statements say or imply otherwise:

- `Foundation/Surl.Authentication.UnitLibrary/AccountBook.cs`, XML doc of `TakeSnapshot`: "never a clear
  password". The class summary already describes the UTF-8 bytes correctly.
- `AccountSnapshot.cs`, class summary: "never a clear password".
- `DerivedAccount.cs`, record summary: "The clear password is not kept."
- That library's `CLAUDE.md`: the "Accounts changed at run time" section says a snapshot holds "hashes and derived
  credentials, never `Account.Password`" without saying some of them are the password's bytes, and the first
  `AccountBook` bullet ("What is here now") names only the SHA-256.
- `Application/Surl.Api/SurlSnapshot.cs`, class summary: "Accounts are held as password hashes, never a clear
  password." BL-1831 rewrites the same summary's store list, hence the dependency.
- `Application/Surl.Api/README.md`, the snapshot text: "accounts as password hashes only".

## Acceptance criteria

- [ ] The XML docs of `AccountBook.TakeSnapshot`, `AccountSnapshot` and `DerivedAccount` say a snapshot or record
      holds the password's SHA-256 and derived credentials, some of which are the password's bytes, and none says
      the clear password is not kept or never held.
- [ ] `Foundation/Surl.Authentication.UnitLibrary/CLAUDE.md` says the same in both places named in Context, and
      that the empty-name account is held as its SHA-256 only.
- [ ] `SurlSnapshot`'s summary and the `Surl.Api` README say the same of accounts and no longer say "password
      hashes only" or "never a clear password".
- [ ] `dotnet build Foundation/Surl.Authentication.UnitTests -warnaserror` and
      `dotnet build Application/Surl.Api -warnaserror` are clean.
- [ ] The only changed lines in `.cs` files are comments.

## Notes

## Log

- 2026-11-27: Created.
