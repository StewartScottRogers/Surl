---
id: BL-1216
title: Decide what reset, snapshot and restore do with in-progress object uploads and the MCP server's OAuth grants
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-061
created: 2026-11-15
completed:
---
# BL-1216 — Decide what reset, snapshot and restore do with in-progress object uploads and the MCP server's OAuth grants

## Goal

An ADR, decided by Claude under Stewart's delegation, audits the state the S3, Azure Storage, Google Cloud Storage, OCI registry and extended MCP servers hold outside `ComposedStores`, and says what a reset, snapshot and restore do with it, as ADR-0159 did for the brokers. The follow-up tasks are filed.

## Context

- Found by the stand-in audit of 2026-11-15. ADR-0124 decision 3 resets, snapshots and restores the shared stores, and ADR-0159 added the brokers' own state. These servers keep state of their own in memory, per server instance, that no reset touches:
  - S3 multipart uploads: `S3MultipartUploads` in `Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary`, up to 1000 open uploads with a 24 h idle lifetime;
  - Azure uncommitted blocks: `AzureStagedBlocks`;
  - GCS resumable and XML multipart uploads: `GcsStagedUploads`;
  - OCI upload sessions: `OciUploadSessions`, up to 1000 with a 1 h lifetime, plus the `OciTokenBook` tokens;
  - the MCP server's OAuth state: `McpAuthorizationGrants`, registered clients, issued tokens and `McpHttpSessionTable`.
- Why it matters for a stand-in: after `ResetAsync`, an upload begun in the previous test can still be completed into the fresh store, and its open uploads count toward the 1000 cap. A token issued to an account that the reset removed may still be accepted.
- Questions:
  1. Classify each piece as configured, test data, or live session state (ADR-0159 decision 1's classes).
  2. Say what reset, snapshot and restore do with each.
  3. Say how each server learns of it: a registration through ADR-0158 decision 2's seam (`IControllableStore`), as `Amqp0ExchangeStoreRegistration` does, never by watching events (ADR-0159 decision 3).
  4. Decide whether a removed account's tokens stop working at once. Secure by default suggests they should.
- Where the staged bytes live (content store temporary files or memory) affects the answer. Read each class before deciding.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/`, with the next free number, marked "Decided by Claude under Stewart's delegation", has an audit table covering every piece listed above and answers questions 1 to 4. It amends ADR-0124 decision 3 (header "Amended by").
- [ ] One feature task per server library that must change is filed with `task-board.ps1 new`, depending on this task, each touching that library, its tests and `Application/Surl.Composition.UnitLibrary` with its tests for the registration. Each is listed in Notes, or Notes says why none is needed.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
