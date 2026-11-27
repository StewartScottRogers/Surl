---
id: BL-1865
title: Correct the control endpoint warning and the Surl.Composition docs for removing the last server account
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1863, BL-1851]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1865 — Correct the control endpoint warning and the Surl.Composition docs for removing the last server account

## Goal

The control endpoint's removal of the last account writes the warning ADR-0132 amendment 5 gives, and
`Surl.Composition` documents no server that stops needing a login.

## Context

- ADR-0132 amendment 5, decision 8.
- `ComposedControlTarget.RemoveAccountAsync` writes one of two old lines, chosen by its `hasDirectory` parameter,
  pinned by `ComposedControlTargetAccountRemovalTests`. One line now serves both, so the parameter goes if nothing
  else reads it.
- `ComposedStores.HasAccounts` says that with no account the server needs no login.

## Acceptance criteria

- [x] Removing the last account writes
      `surl: warning: no local account left; the server still asks for a login wherever it did` once, with and without
      `--user-ldap`, pinned by `ComposedControlTargetAccountRemovalTests`.
- [x] A removal that leaves an account writes no line (existing or new named test).
- [x] No constructor parameter of `ComposedControlTarget` is left unread.
- [x] The XML documents of `ComposedStores.HasAccounts` and `ComposedControlTarget`, and the library's `CLAUDE.md`,
      say `false` means no account is held; none says the server needs no login.

## Notes

- 2026-11-27: Removed the `hasDirectory` parameter and its argument in `ComposedServing`; nothing else read it. One
  line is now written, as ADR-0132 amendment 5 decision 8 gives it, so no new ADR.
- `RemoveAccountAsync_TheLastAccount_WritesTheWarningOnce` runs with and without `--user-ldap` (both cases failed on
  the old text first); the directory is never contacted. A removal that leaves an account:
  `RemoveAccountAsync_ANonLastAccountOrAnUnknownName_WritesNothing`.
- `SurlServerCompositionTests.Control.cs` also pinned the old line; its test is renamed to
  `ServeAsync_ControlDeleteOfTheLastAccount_Answers204AndLogsTheNoLocalAccountLeftWarning`.
- `Surl.Api` still writes the old lines; that is BL-1866.
- Verify: build 0 warnings, format clean, fast tests 30270 passed, 0 failed, 3 skipped. `Integration` tests not run:
  no network, file system or database code changed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The control endpoint's removal of the last account now warns that the server still asks for a login, and Surl.Composition documents HasAccounts false as no account held.
