---
id: BL-2104
title: Add a dated note to ADR-0158 decision 6 on what a host can do to a directory or zones loaded from --directory
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2076]
touches: [Documentation/Planning/Decisions/ADR-0158-how-the-redis-keyspace-the-ldap-directory-and-the-dns-zones-are-seeded-reset-snapshotted-restored-and-observed.md]
requirement: FR-061
created: 2026-11-28
completed:
---
# BL-2104 — Add a dated note to ADR-0158 decision 6 on what a host can do to a directory or zones loaded from --directory

## Goal

A reader of ADR-0158 learns from a dated note whether a test can reset, snapshot and restore a `--directory`-loaded LDAP directory or DNS zones through `Surl.Api`, `surl-mcp` and the control endpoint. No behaviour changes.

## Context

Found while working BL-2090 (checked by reading the code on 2026-11-28). ADR-0158 decision 6 says reset, snapshot and
restore are allowed on a directory and zones loaded from `--directory`, and its "Alternatives considered" rejects
refusing the reset because it "would leave a test that mounts a directory with no way to reset an LDAP or DNS
stand-in". In the code:

- Only `ComposedStores.ResetAsync(StoreKind, ...)` and `TakeSnapshotAsync(StoreKind, ...)`
  (`Application/Surl.Composition.UnitLibrary/ComposedStores.cs`) act on one store, and no host calls them.
- `SurlServer.ResetAsync` and `TakeSnapshotAsync` (`Application/Surl.Api/SurlServer.cs`), `surl-mcp`'s
  `ResetServerTool` (which calls `SurlServer.ResetAsync`) and `ComposedControlTarget.ResetAsync` and its snapshot do
  the whole-instance operation only.
- That operation walks the stores in order, the content store first, and stops with a `ComposedStoreException` when
  the content store is on disk, so with `--directory` a host reaches neither the directory nor the zones, and has no
  snapshot to restore.

BL-2076 decides whether the three hosts gain a one-store reset, snapshot and restore or FR-061 stops claiming it, and
files the code tasks if they are to be built. It leaves ADR-0158 out of its acceptance criteria. This task does not
decide that again: it records BL-2076's outcome where ADR-0158's reader looks. If BL-2076 has already added a note to
ADR-0158 that states all of the criteria below, tick them against that note and add nothing.

Stand-in: the note says what a test can and cannot reset on a mounted directory; this task changes no store, event,
seed, reset or fault.

## Acceptance criteria

- [ ] ADR-0158 has an amendment, dated the day it is written, marked "Decided by Claude under Stewart's delegation", naming this task and linking the ADR in which BL-2076 recorded its decision.
- [ ] The amendment says, for `Surl.Api`, `surl-mcp` and the control endpoint, whether each can reset, snapshot and restore the directory and the zones of a server given `--directory`, as the code is on that day, with anything BL-2076 decided to build and not yet built written as intent with its task IDs.
- [ ] Decision 6 and the "Alternatives considered" bullet about treating a `--directory` copy as a disk store each point to the amendment.
- [ ] The diff changes no file but ADR-0158 (and this task's own), and rewrites none of the original sentences.

## Notes

## Log

- 2026-11-28: Created.
