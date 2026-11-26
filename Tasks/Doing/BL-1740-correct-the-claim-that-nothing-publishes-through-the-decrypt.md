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
completed:
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

- [ ] The bullet no longer says nothing publishes through the contract, and what it says in its place is true of the code.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
