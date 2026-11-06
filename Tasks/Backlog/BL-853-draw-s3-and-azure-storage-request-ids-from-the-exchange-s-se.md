---
id: BL-853
title: Draw S3 and Azure Storage request IDs from the exchange's seedable identifier source
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822, BL-851]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-853 — Draw S3 and Azure Storage request IDs from the exchange's seedable identifier source

## Goal

S3's `x-amz-request-id` and Azure Storage's `x-ms-request-id` come from `ExchangeContext.Identifiers`, so a
seeded instance answers the same request IDs every run.

## Context

- Decision: ADR-0124 decision 6 names request IDs as seedable (they grant nothing). Found by BL-786's audit.
- `Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary/S3ProtocolServer.cs:81`,
  `S3RequestAnswerer.cs:51` and `:95` - `RandomNumberGenerator.GetHexString(16)`.
- `Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary/AzureReply.cs:78` -
  `AzureReply.NewRequestId()` builds a GUID from `RandomNumberGenerator.GetBytes(16)`.
- Fix: draw the same number of bytes from `context.Identifiers` and keep each ID's shape (16 hex digits for S3, a
  `D`-format GUID for Azure), so nothing on the wire changes form.

## Acceptance criteria

- [ ] No `RandomNumberGenerator` call is left in either library for a request ID.
- [ ] `S3RequestAnswererTests.RequestId_SameSeededIdentifiers_IsTheSameEveryRun` and
      `AzureReplyTests.RequestId_SameSeededIdentifiers_IsTheSameEveryRun` pass: two exchanges given
      `SeededIdentifierRandom` sources with the same seed and exchange ID answer equal request IDs; different seeds
      answer different ones.
- [ ] Both libraries stay at 100% line and branch coverage; `dotnet build` clean, fast tests green.

## Notes

## Log

- 2026-11-05: Created.
