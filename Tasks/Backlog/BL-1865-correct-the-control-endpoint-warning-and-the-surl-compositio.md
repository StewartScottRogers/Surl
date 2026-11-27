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
completed:
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

- [ ] Removing the last account writes
      `surl: warning: no local account left; the server still asks for a login wherever it did` once, with and without
      `--user-ldap`, pinned by `ComposedControlTargetAccountRemovalTests`.
- [ ] A removal that leaves an account writes no line (existing or new named test).
- [ ] No constructor parameter of `ComposedControlTarget` is left unread.
- [ ] The XML documents of `ComposedStores.HasAccounts` and `ComposedControlTarget`, and the library's `CLAUDE.md`,
      say `false` means no account is held; none says the server needs no login.

## Notes

## Log

- 2026-11-27: Created.
