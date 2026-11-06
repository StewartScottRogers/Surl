---
id: BL-802
title: Add, reset, snapshot and restore accounts at run time in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-791]
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-802 — Add, reset, snapshot and restore accounts at run time in Surl.Authentication

## Goal

A test can add or remove an account (user name, password, and its protocol restriction) on a running server,
return the account set to the configured accounts between tests, and take and restore a snapshot of it, each
published on the event stream, with every login of every protocol judged by the changed set at once.

## Context

- Decision: BL-822's ADR decision 3 (accounts as a store: what reset returns to - planning default: exactly the
  accounts from `--user`, `--user-file`, `--mailbox` and groups at start; whether a snapshot holds password hashes
  only - it never holds a clear password; whether a removed account's open sessions end).
- Where: `Foundation/Surl.Authentication.UnitLibrary` - `AccountBook.cs` (today filled once at start; it keeps
  password hashes and a dummy hash so timing does not reveal unknown users) and `AuthenticationPolicy.cs`. Make
  changes thread-safe against logins in flight. Every secret still compared with
  `CryptographicOperations.FixedTimeEquals`; the 1-second refusal delay unchanged (FR-027). Accounts that carry
  other credentials (Digest, NTLM, SigV4, SCRAM - `DigestAccount`, `NtlmAccount`, `AwsSigV4Account`,
  `ScramCredentials`) are derived for an added account exactly as for one read at start.
- Publishes `StoreChangedEvent` (accounts) with the user name only. Adding an account is a loosening; the ADR
  says whether it is logged as a warning.
- Stand-in: store = the account set (ADR-0100 decision 4); controllable. Composition exposes it in BL-805;
  `Surl.Api` in BL-810.

## Acceptance criteria

- [ ] Named tests: an added account logs in over a password login, HTTP Basic, HTTP Digest and SASL PLAIN; a
      removed account is refused like an unknown user (same verdict, note and delay); reset restores the start
      accounts; snapshot, change, restore gives back the same set; events carry no secret.
- [ ] A named test runs 32 concurrent logins while an account is added and removed and sees no exception and only
      the two verdicts the ADR allows.
- [ ] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` is clean, the whole solution builds,
      the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Lane 5 finished and verified it but could not push during the 16:25 restart; the work is on local branch factory/BL-802-lane-5-20261105-150047 (6987d83f) - rebase it onto the shift branch, rebuild, test and push rather than redo it
