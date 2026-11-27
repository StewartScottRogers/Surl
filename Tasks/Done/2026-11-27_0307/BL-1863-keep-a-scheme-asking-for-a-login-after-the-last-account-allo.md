---
id: BL-1863
title: Keep a scheme asking for a login after the last account allowed on it is removed
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-1854, BL-1850]
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests, Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs, Application/Surl.Composition.UnitLibrary/CLAUDE.md, Application/Surl.Composition.UnitTests/CoapsCompositionTests.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1863 — Keep a scheme asking for a login after the last account allowed on it is removed

## Goal

A scheme that has had an account to log in to keeps asking for a login when none is left, until a reset or restore
returns the accounts to a state in which it never had one.

## Context

- ADR-0132 amendment 5, decisions 1, 2, 3, 4 and 9. It is the specification.
- `HttpAuthenticationSession.JudgeWithoutCredentials` and `ExtendedProtocolServers.CoapAccessRulesFor` ask
  `AuthenticationSettings.HasAccountToLogInTo`, which reads `AccountBook.HasAccounts` of the scheme view, so reads
  open when the last account is removed.
- Rename that method to `IsLoginRequiredToRead`. `AccountStore` (with `AccountSnapshot`) remembers which schemes the
  accounts it has held may log in on, and keeps no credential of a removed account for it. `AccountBook.HasAccounts`
  still means an account is held now. The `CoapAccessRules.HasAccounts` rename is BL-1864.

## Acceptance criteria

- [x] Named tests show that a `GET` without credentials is answered `Challenge` after the last account is removed,
      both for a book built with that account and for a book built empty that had it added.
- [x] A named test shows that removing the only account allowed on `http` while a mail-only account remains leaves the
      `http` view challenging such a `GET`, and that a view of a scheme that never had an account still proceeds.
- [x] Named tests show that `Reset` makes a book built empty proceed again and leaves a book built with an account
      challenging, and that `Restore` returns the answer that held when the snapshot was taken.
- [x] A named test shows that `AllowAnonymous` and a connection NTLM logged in still proceed after the removal, and
      the existing test that a book that never held an account proceeds on `GET` still passes.
- [x] `HasAccountToLogInTo` appears in no `.cs` or `CLAUDE.md` file under `Foundation/` or `Application/`.
- [x] The XML documents of the renamed method, `AccountBook.HasAccounts` and `AccountSnapshot`, and the library's
      `CLAUDE.md` (its per-scheme account views section included), state the rule; none says that a book with no
      account left serves anonymous reads.

## Notes

- Built as ADR-0132 amendment 5 decision 9 says; no new decision, so no new ADR, and no new byte to measure
  (decision 10).
- `AccountSet.HeldSchemes` is the memory: the different `Account.Schemes` values of every account held, copied, with
  no name and no credential. It sits in the set that is swapped whole, so a read sees accounts and memory together,
  and a scheme view filters it as it filters accounts. `IsLoginRequiredToRead` asks the internal
  `AccountBook.HasHeldAccount`.
- Added `Application/Surl.Composition.UnitTests/CoapsCompositionTests.cs` to `touches`: its test pinned the old
  answer (reads open after the removal). No task in Doing names that project. Renamed to
  `CoapAccessRulesFor_AccountAddedAndRemovedAfterComposing_StaysClosedUntilAReset`.
- Named tests: the `JudgeAsync_After*`, `JudgeAsync_AllowAnonymousAfter*` and `JudgeAsync_OnAConnectionNtlm*` tests in
  `HttpAuthenticationSessionTests`, and the `IsLoginRequiredToRead_*` tests in `AuthenticationSettingsTests`.
- Gates: build with 0 warnings, format clean, fast tests 30253 passed, 0 failed, 3 skipped;
  `Measure-CodeQuality.ps1 -Library Surl.Authentication.UnitLibrary` reports 0 failing members. No integration run:
  nothing here touches the network or the file system.
- The "needs no login" warnings of the control endpoint, `Surl.Api` and `surl-mcp` and `CoapAccessRules.HasAccounts`
  are unchanged here: BL-1864, BL-1865, BL-1866 and BL-1859.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. A scheme that has had an account keeps asking for a login after its last account is removed, until a reset or restore returns the accounts to a state in which it never had one
