---
id: BL-1286
title: Surl.Mcp reads and writes every SurlEvent through its members and derives the kinds enum (ADR-0171)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1245, BL-1283]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: FR-060
created: 2026-11-16
completed: 2026-11-16
---
# BL-1286 — Surl.Mcp reads and writes every SurlEvent through its members and derives the kinds enum (ADR-0171)

## Goal

get_events and wait_for_event filter by every SurlEvent kind and RecordedEventWriter writes every event in full through the base members; the kinds enum in the schema is derived.

## Context

ADR-0171 (BL-1245) decides how a SurlEvent kind reaches the three hosts: the event says what hosts read; hosts keep no per-type switch.

## Acceptance criteria

- [x] EventToolFilter's kinds enum is built from the exported SurlEvent subclasses; RecordedEventWriter uses WriteMembers and the base ExchangeId.
- [x] A test enumerating the exported subclasses fails when one is missing from the kinds enum, not matched by IsOfKind, or any public property is missing from its written output.
- [x] Build clean, fast tests green, 100% line and branch coverage on the touched libraries.

## Notes

- `GetExportedTypes` is IL2026 (trim/AOT), so `EventToolFilter.ReadAllKinds` names the records with `typeof`; `RecordedEventWriterTests` enumerates the exported subclasses and fails on one not in the kinds enum, not matched by `IsOfKind`, without a sample, or with a property `WriteMembers` skips.
- A member named `Kind` would collide with the head's `kind`, so it is written `fault_kind` (first word of the kind + `_kind`), keeping today's bytes. Member order now follows each record's `WriteMembers`; `exchange_bytes` is written in full (hex, 256 bytes max).

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Mcp reads and writes every SurlEvent through its members
