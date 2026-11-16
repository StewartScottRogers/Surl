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
completed:
---
# BL-1286 — Surl.Mcp reads and writes every SurlEvent through its members and derives the kinds enum (ADR-0171)

## Goal

get_events and wait_for_event filter by every SurlEvent kind and RecordedEventWriter writes every event in full through the base members; the kinds enum in the schema is derived.

## Context

ADR-0171 (BL-1245) decides how a SurlEvent kind reaches the three hosts: the event says what hosts read; hosts keep no per-type switch.

## Acceptance criteria

- [ ] EventToolFilter's kinds enum is built from the exported SurlEvent subclasses; RecordedEventWriter uses WriteMembers and the base ExchangeId.
- [ ] A test enumerating the exported subclasses fails when one is missing from the kinds enum, not matched by IsOfKind, or any public property is missing from its written output.
- [ ] Build clean, fast tests green, 100% line and branch coverage on the touched libraries.

## Notes

## Log

- 2026-11-16: Created.
