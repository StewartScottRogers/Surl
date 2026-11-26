---
id: BL-1737
title: Correct the claim that nothing implements the received-bytes redaction contract
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-25
completed:
---
# BL-1737 — Correct the claim that nothing implements the received-bytes redaction contract

## Goal

The `Surl.Protocol.Abstractions.UnitLibrary` project instructions say truthfully what implements the received-bytes
redaction contract.

## Context

- Found by `align-and-document` while delivering BL-1729.
- `Foundation/Surl.Protocol.Abstractions.UnitLibrary/CLAUDE.md`, the bullet on the received-bytes redaction contract
  (ADR-0182, decision 6), ends "Nothing implements them yet." `Http2RequestBytesRedactor` implements
  `IReceivedBytesRedactor`, and `GrpcProtocolServer` has `CreateRedactor`, so the sentence is false.

## Acceptance criteria

- [ ] The bullet no longer says nothing implements the contract, and what it says in its place is true of the code.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
