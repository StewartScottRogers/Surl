---
id: BL-191
title: Persist the mail store under the data directory's .surl/mail folder
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-190]
touches: [Surl.MailStore.UnitLibrary, Surl.MailStore.UnitTests]
requirement: FR-047
created: 2026-09-29
completed:
---
# BL-191 — Persist the mail store under the data directory's .surl/mail folder

## Goal

With a data directory, the mail store writes every change under `<path>/.surl/mail` in BL-184's
ADR's byte format through `IContentFileSystem` and loads it at start, refusing a malformed store
with a typed failure; without one it stays in memory, so mail survives a restart (FR-047).

## Context

- Decisions: BL-184's ADR (byte format, write granularity, temporary name, load and refusal
  rules); ADR-0031 decision 6 (the pattern: whole-file or per-record writes through a temporary
  name and `IContentFileSystem.MoveFileReplacing`, serialised writes, a failed write noted with
  `IExchangeLog.Note` and not fatal, loaded once at start, leftovers from a crash ignored, a
  malformed file refused rather than overwritten) and decision 8 (nothing outside the data
  directory).
- The model to copy: `Surl.Protocol.Mqtt.UnitLibrary/MqttRetainedMessageFile.cs` and
  `MqttRetainedMessages.LoadAsync` (BL-094). `Surl.Console` loading the store at start and
  turning the failure into `CouldNotReadFile` (37) with its text is BL-207's.
- Tests use `Surl.Content.UnitLibrary/InMemoryContentFileSystem.cs`.

## Acceptance criteria

- [ ] Fast tests pin the persisted bytes for an empty store and for a store with two mailboxes and
      three messages, exactly as the ADR's format says.
- [ ] Fast tests show: a store written then loaded holds the same mailboxes, messages, flags, UIDs
      and `UIDVALIDITY`; every malformed case the ADR lists refused with its reason; a missing
      store loaded empty; a write that throws leaves the store changed in memory and is reported,
      and the next change rewrites what the format needs; a leftover temporary file ignored.
- [ ] `dotnet build Surl.MailStore.UnitLibrary -warnaserror` is clean; the fast tests pass;
      `Measure-CodeQuality.ps1 -Library Surl.MailStore.UnitLibrary` reports 100% line and branch
      coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
