---
id: BL-1509
title: Compose configured folder uses into the mail store and its mailbox listing
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1507, BL-1508]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1509 — Compose configured folder uses into the mail store and its mailbox listing

## Goal

The configured folder uses reach the mail store when a start composes it, and the composed mailbox listing reports each mailbox's use.

## Context

ADR-0055 Amendment 1, decisions 2 and 6. Depends on the store's `MailboxSettings.FolderUses` (BL-1507) and the parsed `ConfiguredMailbox` uses (BL-1508). Code: `Application/Surl.Composition.UnitLibrary/MailboxComposition.cs` (maps configured mailboxes to `MailboxSettings`) and `ComposedStores.cs` (`ListMailboxes`, `ComposedMailboxInfo`). `ComposedMailboxInfo` gains a nullable `Use` (an added field, nothing repurposed) that Surl.Api, the control endpoint and surl-mcp read in their own tasks (BL-1512, BL-1513, BL-1514).

## Acceptance criteria

- [x] `MailboxComposition` copies a configured mailbox's folder uses into `MailboxSettings.FolderUses`; a mailbox with none maps to an empty map as today.
- [x] `ComposedMailboxInfo` has a `Use` that is null for a mailbox without one and the use text for one with it.
- [x] `ComposedStores.ListMailboxes` fills it from the store.
- [x] A composed start with a use configured, then a store reset, lists the use again; a use gained by `CREATE` is gone after the reset.
- [x] Tests exist and pass, coverage stays 100%, and `dotnet build` of both projects with `-warnaserror` is clean.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. Composition copies folder uses into MailboxSettings and ComposedMailboxInfo.Use lists them; test covers reset
