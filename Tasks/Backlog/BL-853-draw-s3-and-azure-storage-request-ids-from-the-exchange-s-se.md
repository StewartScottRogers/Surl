---
id: BL-853
title: Draw S3 and Azure Storage request IDs from the exchange's seedable identifier source
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822, BL-851, BL-1035]
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

- 2026-11-11 (lane 4): not started in code - `dotnet` cannot resolve the pinned SDK 10.0.401 (installed: 10.0.100-rc.1,
  11.0.100-rc.2, 11.0.100), so nothing can be built or tested; the task waits on BL-1035. Design, ready to apply:
  - Per-request IDs (`S3RequestAnswerer.AnswerAsync`, `RefuseHeadNotRead`; `AzureBlobRequestAnswerer`,
    `AzureDataLakeRequestAnswerer`; `AzureReply.HeadNotReadRefusal`, which gains an `IIdentifierRandom` parameter) draw
    from `context.Identifiers`: S3 8 bytes rendered as 16 upper-case hex digits (`Convert.ToHexString`, the shape
    `RandomNumberGenerator.GetHexString(16)` gives); Azure 16 bytes into `new Guid(bytes).ToString("D")`.
    `AzureReply.NewRequestId()` becomes `NewRequestId(IIdentifierRandom)`; S3 gets an internal `S3RequestId.Draw`.
  - Connection refusals (`WriteRefusalAsync`) have no exchange, so each server gains a last optional constructor
    parameter `IIdentifierRandom? identifiers = null`, as BL-1000 does for NATS, so `Surl.Composition` compiles
    unchanged. Null defaults to an internal per-library source filling from `RandomNumberGenerator` (a protocol server
    may not reference `Surl.Core`'s `CryptographicIdentifierRandom`, ADR-0002). Wiring a seeded server-life source
    (`SeededIdentifierRandom.ForServer("s3")`) through Composition is a follow-up for whoever plumbs `--random-seed`.
  - Tests: both test projects reference `Surl.Core.UnitLibrary` for `SeededIdentifierRandom(seed).ForExchange(id)`.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet cannot build or test
