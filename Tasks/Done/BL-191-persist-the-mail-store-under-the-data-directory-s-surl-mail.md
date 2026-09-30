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
completed: 2026-09-29
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

- [x] Fast tests pin the persisted bytes for an empty store and for a store with two mailboxes and
      three messages, exactly as the ADR's format says.
- [x] Fast tests show: a store written then loaded holds the same mailboxes, messages, flags, UIDs
      and `UIDVALIDITY`; every malformed case the ADR lists refused with its reason; a missing
      store loaded empty; a write that throws leaves the store changed in memory and is reported,
      and the next change rewrites what the format needs; a leftover temporary file ignored.
- [x] `dotnet build Surl.MailStore.UnitLibrary -warnaserror` is clean; the fast tests pass;
      `Measure-CodeQuality.ps1 -Library Surl.MailStore.UnitLibrary` reports 100% line and branch
      coverage and no failing member.

## Notes

- Built: `MailStoreFiles` (paths, reads, writes through a temporary name and
  `MoveFileReplacing`), `MailStoreIndex` (ADR-0050 decision 7's byte format, encode and
  decode with every refusal the ADR lists), `MailStoreLoadException` (the file at fault and
  the reason, for BL-207's `CouldNotReadFile` text), and `MailboxStore.LoadAsync` /
  `SaveChangesAsync`, the pattern of `MqttRetainedMessages` (BL-094).
- Decision (sensible default, within ADR-0050): the store changes in memory and the server
  calls `SaveChangesAsync` after each change, as MQTT does, rather than every operation
  becoming async. A save writes each new message file, then the whole index, then deletes
  files of messages no longer held; it throws the file system's exception for the server to
  note with `IExchangeLog.Note`. A failed message file or index write leaves the change in
  memory and the next save writes what is still needed (the acceptance criterion); a failed
  delete is reported as an `IOException` and the file is left behind, ignored at load.
- Decision: every message's bytes are still held in memory, loaded from their files at start,
  and a message file is written by the save rather than streamed into its pending file as the
  server reads the body. ADR-0050 decision 7's streaming, its `StorageFailed` refusal and
  read-on-fetch change the API the servers use, so they are BL-226 (filed), not widened into
  this task. The library's CLAUDE.md states it as intent.
- Decision: message file numbers are given from 0 by every store, persisted or not, so an
  in-memory store's numbering matches what it would write; an `INBOX` created at load counts
  as a change so the first save writes the index, while the constructor's store starts at
  `ChangeCount` 0 as before.
- The index's internal date is refused when it (or it with its offset) lies outside
  `DateTimeOffset`'s range, a "truncated part" in effect; an index owner with no mailboxes is
  accepted and not written back, as the ADR says of writing.
- Tests: `MailStoreIndexTests` pins the empty and the two-mailbox, three-message bytes by
  hand; `MailboxStoreLoadTests` covers the 27 malformed indexes, three bounds, message file
  refusals and unreadable files; `MailboxStoreSaveTests` covers round trips, unreached and
  anonymous owners, file deletion, and write, index and delete failures. 208 tests in
  `Surl.MailStore.UnitTests`; `Measure-CodeQuality.ps1 -Library Surl.MailStore.UnitLibrary`:
  100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. The mail store persists under .surl/mail: LoadAsync reads the index and message files, refusing a malformed store with MailStoreLoadException; SaveChangesAsync writes every change
