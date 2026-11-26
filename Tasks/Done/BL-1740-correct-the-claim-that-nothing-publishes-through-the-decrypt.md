---
id: BL-1740
title: Correct the claim that nothing publishes through the decrypted-bytes contract
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1740 — Correct the claim that nothing publishes through the decrypted-bytes contract

## Goal

The `Surl.Protocol.Abstractions.UnitLibrary` project instructions say truthfully what publishes through
`IDecryptedBytesSink`.

## Context

- Found while delivering BL-1737.
- `Foundation/Surl.Protocol.Abstractions.UnitLibrary/CLAUDE.md`, the bullet on the decrypted-bytes contract
  (ADR-0169), ends "Nothing publishes through it yet." `DtlsServerSession`, `QuicServerConnection`, `QuicStream`
  and `Http3RequestStreamServer` call `DecryptedBytes.Received` or `Sent`, so the sentence is false.

## Acceptance criteria

- [x] The bullet no longer says nothing publishes through the contract, and what it says in its place is true of the code.

## Notes

- The false sentence now names the sink `ServingEngine` sets (`EventPublishingDecryptedBytesSink`), what it does,
  every non-test caller of `Received` or `Sent`, and which servers hand the sink on. The callers were found by
  searching the code; the Context list missed `Http3RequestStream`. No ADR: no decision was needed.
- Verified 2026-11-25: `dotnet build` clean, fast tests pass (29,315 passed).
- Follow-up filed as BL-1741 (doc comments in `.cs` files, outside this task's `touches`). Filed with the board
  script directly, as it is one small task.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. The Abstractions instructions now say what publishes through IDecryptedBytesSink
