---
id: BL-1156
title: Decide whether the S3 UploadId, the OCI upload session UUID and the GCS upload ID follow --random-seed
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-064
created: 2026-11-14
completed:
---
# BL-1156 — Decide whether the S3 UploadId, the OCI upload session UUID and the GCS upload ID follow --random-seed

## Goal

A new ADR, marked "Decided by Claude under Stewart's delegation", classifies three generated upload identifiers under ADR-0124 decision 6 (seedable non-secret identifier, or secret that stays on `RandomNumberGenerator`), and files the fix task for each one that must follow `--random-seed`.

## Context

- Stand-in audit 2026-11-14 (determinism). ADR-0124 decision 6: generated identifiers that grant nothing follow the instance's `IIdentifierRandom` (`ExchangeContext.Identifiers`, `ComposedProtocolServices.IdentifiersForServer`); keys, nonces, session tokens and anything that grants access never do. Three upload identifiers are drawn unseeded today:
  - S3 multipart `UploadId`: `RandomNumberGenerator.GetHexString(32, ...)` in `Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary/S3CreateMultipartUpload.cs`. Every S3 request is SigV4-signed, so the ID alone grants nothing - but check the S3 server's own code before deciding.
  - OCI upload session UUID: `Guid.NewGuid` (`NewRandomSessionId` in `Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary/OciRegistryProtocolServer.cs`), chosen by ADR-0130 decision 7. Requests still need the bearer token from `OciTokenBook`.
  - GCS resumable upload ID: `RandomNumberGenerator.GetHexString(32, ...)` in `Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/GcsUploadStaging.cs`. Google's resumable-upload documentation treats the session URI as an authentication token, which points to "secret".
- Judge each by its own specification (ADR-0083) and by what Surl's server checks when the ID comes back.

## Acceptance criteria

- [ ] A new ADR under `Documentation/Planning/Decisions/` (next free number) classes each of the three identifiers as seedable or secret, with the reason drawn from the service's documentation and Surl's code.
- [ ] Where it reverses ADR-0130 decision 7, ADR-0130 gains an "Amended by" line.
- [ ] For each identifier decided seedable, this task files one fix task with `task-board.ps1 new` (`touches` the server's `.UnitLibrary` and `.UnitTests`, plus `Application/Surl.Composition.UnitLibrary` and `Application/Surl.Composition.UnitTests` if the source must be passed in; `depends-on` this task; requirement FR-064), whose criteria include a test that two instances with the same `RandomSeed` give the same identifier for the same exchange order; the ADR lists those tasks under "Acted on by".

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
