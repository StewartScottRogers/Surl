---
id: BL-1499
title: Add the DeliveredFor member to StoreChangedEvent
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1487]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1499 — Add the DeliveredFor member to StoreChangedEvent

## Goal

`StoreChangedEvent` carries an optional `DeliveredFor` list of envelope addresses, written to every event rendering, so a test can tell mail copies apart.

## Context

ADR-0053 Amendment 1 decision 7: the mail store's delivery event gains `DeliveredFor`, the envelope addresses (as sent, in `RCPT` order) that resolved to the owner of the copy; an optional member older subscribers ignore, set whatever the option. `Foundation/Surl.Protocol.Abstractions.UnitLibrary/StoreChangedEvent.cs` already has an `init`-only optional `PreviousName` (ADR-0129): add `DeliveredFor` beside it the same way (`IReadOnlyList<string>?`, null when none apply), and write it in `WriteMembers` through `ISurlEventMemberWriter` (check which writer method fits a list; add one only if none does, and keep every implementer building). `SurlEventMemberTests` fails if a public property is left unwritten. Stores other than mail leave it null. Populating it is BL-1500.

## Acceptance criteria

- [ ] `StoreChangedEvent.DeliveredFor` exists, is `null` by default, and has an XML doc comment citing ADR-0053 Amendment 1.
- [ ] `WriteMembers` writes it, and a test shows the written member for a non-null list and for `null` (matching how `PreviousName` is handled).
- [ ] `SurlEventMemberTests` passes and an event built without `DeliveredFor` renders exactly as before, pinned by a test.
- [ ] `dotnet build Foundation/Surl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean and the `Surl.Protocol.Abstractions.UnitTests` fast tests pass at 100% line and branch coverage for the change.
- [ ] `Foundation/Surl.Protocol.Abstractions.UnitLibrary/CLAUDE.md` names the new member in its `StoreChangedEvent` line.

## Notes

## Log

- 2026-11-20: Created.
