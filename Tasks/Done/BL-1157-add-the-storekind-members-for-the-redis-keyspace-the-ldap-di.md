---
id: BL-1157
title: Add the StoreKind members for the Redis keyspace, the LDAP directory and the DNS zones
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1151]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: FR-060
created: 2026-11-14
completed: 2026-11-14
---
# BL-1157 — Add the StoreKind members for the Redis keyspace, the LDAP directory and the DNS zones

## Goal

`StoreKind` in `Surl.Protocol.Abstractions` has the members BL-1151's ADR names for the Redis keyspace, the LDAP directory and the DNS zones, appended after `Accounts`, so the three stores can publish `StoreChangedEvent`.

## Context

- Contract first: `Foundation/Surl.Protocol.Abstractions.UnitLibrary/StoreKind.cs` (today `Content, Mail, Bus, Accounts`, ADR-0124 decision 1). The enum only grows; existing members keep their names and numeric values.
- Consumers to keep green, not to change: `Application/Surl.Control.UnitLibrary/ControlEventWriter.cs` and `ControlEventKinds.cs`, `Application/Surl.Mcp.UnitLibrary/RecordedEventWriter.cs`, `Application/Surl.Api/SurlEventQuery.cs` and `SurlEventFacts.cs` write or match store kinds. If any writer maps kinds through an exhaustive switch the build would break on; that is the moment to find out, and the fix (a name derived from the member) belongs here only if it is one line per writer - otherwise file a follow-up task.
- If `StoreChange` needs a new member by the ADR (for example a key expiring), add it here too, appended.

## Acceptance criteria

- [x] `StoreKind` gains exactly the members BL-1151's ADR names, after `Accounts`, each with an XML doc comment naming its store.
- [x] A test in `Foundation/Surl.Protocol.Abstractions.UnitTests` pins every `StoreKind` member's name and numeric value, so a reorder fails.
- [x] `dotnet build Surl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green, the control endpoint's and `Surl.Mcp`'s event-writer tests included.
- [x] `Surl.Protocol.Abstractions.UnitLibrary` keeps 100% line and branch coverage.

## Notes

ADR-0158 decision 1 names Keyspace, Directory, Zones; it needs no new StoreChange member. No writer maps kinds through an exhaustive switch; build and all fast tests green. Coverage: enum only, no branches.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. StoreKind gains Keyspace, Directory and Zones after Accounts, pinned by StoreKindTests
