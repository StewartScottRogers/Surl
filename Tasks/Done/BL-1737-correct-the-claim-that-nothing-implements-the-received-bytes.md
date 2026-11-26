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
completed: 2026-11-25
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

- [x] The bullet no longer says nothing implements the contract, and what it says in its place is true of the code.

## Notes

- Checked against the code with `git grep IExchangeBytesRedactingServer` and `ServingEngine.OpenExchange`.
- The replacement names examples rather than every implementer, so a new server does not make the bullet false.
- Edited directly instead of delegating to `align-and-document`: one sentence in one file, already investigated.
- `dotnet build`: 0 warnings, 0 errors. Fast tests: 29299 passed in 83 assemblies, no failures.
- The decrypted-bytes bullet in the same file ends "Nothing publishes through it yet", also false: filed as BL-1740.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. The Abstractions project instructions now say what implements the received-bytes redaction contract.
