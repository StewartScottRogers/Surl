---
id: BL-802
title: Add, reset, snapshot and restore accounts at run time in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-791]
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-05
completed: 2026-11-05
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

- [x] Named tests: an added account logs in over a password login, HTTP Basic, HTTP Digest and SASL PLAIN; a
      removed account is refused like an unknown user (same verdict, note and delay); reset restores the start
      accounts; snapshot, change, restore gives back the same set; events carry no secret.
- [x] A named test runs 32 concurrent logins while an account is added and removed and sees no exception and only
      the two verdicts the ADR allows.
- [x] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` is clean, the whole solution builds,
      the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- Plan: `AccountBook` now reads an internal `AccountStore` that holds an unchanging `AccountSet`, swapped
  whole under a lock (`DerivedAccount` and `NamedAccountCredentials` hold what each password derives). Every
  `ForScheme` view is live over the same store, filtered again only when the set changes. Add, Remove, Reset,
  TakeSnapshot and Restore are on `AccountBook`, and `AccountSnapshot` is public. Recorded in ADR-0132.
- Decisions (ADR-0132, decided by Claude under Stewart's delegation): reset returns to the accounts the book
  was built with; a snapshot holds hashes and derived credentials, never a clear password; a removed
  account's open sessions run on; adding an account is not warned again (the test surfaces are already
  warned); an add publishes `AccountAdded`, not `Seeded`; adding a name that exists throws; authorized keys
  are not part of the account set.
- `AuthenticationSettings.ForScheme` and `AccountBook.ForScheme` no longer return the receiver. Two
  `SchemeAccountViewTests` that pinned `AreSame` now pin the same accounts and keys instead, and new tests
  show a later-added restricted account is left out of an earlier view.
- Touches widened to `Documentation/Planning/Decisions` (ADR-0132 and its README row): the task's decisions
  need an ADR, and no task in Doing names that folder.
- Tests: `AccountBookTests.Changes.cs` (`Add_Then*` for password, HTTP Basic, HTTP Digest and SASL PLAIN;
  `Remove_ThenLogins_AreRefusedLikeAnUnknownUser`; `Reset_RestoresExactlyTheConfiguredAccounts`;
  `SnapshotChangeRestore_GivesBackTheSameSet`; `Changes_ArePublishedInOrderNamingAtMostTheUserAndNoSecret`;
  `ThirtyTwoConcurrentLogins_WhileTheAccountIsAddedAndRemoved_SeeOnlyAcceptedOrRefused`).
- Coverage: every file this task changed or added is at 100% line and branch. The library still lists
  10 members that were failing before this task and are in files it did not touch: complexity in the Azure
  and GSSAPI members, `HttpAuthenticationSession.AnswerCheckAsync` at 90% branch, and the generated regex code.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Lane 5 finished and verified it but could not push during the 16:25 restart; the work is on local branch factory/BL-802-lane-5-20261105-150047 (6987d83f) - rebase it onto the shift branch, rebuild, test and push rather than redo it
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Lane 1 cherry-picked lane 5's commits (03ca1d00, 6987d83f) onto the shift branch; ADR already renumbered 0132 after BL-799 took 0131.
- 2026-11-05: Doing -> Done. Accounts can be added, removed, reset, snapshotted and restored at run time, judged at once through every live scheme view and published without secrets
