---
id: BL-418
title: Create the S3 server and answer GetObject and HeadObject
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-417, BL-404, BL-407]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed:
---
# BL-418 — Create the S3 server and answer GetObject and HeadObject

## Goal

`Surl.Extended.Protocol.S3.UnitLibrary` exists and answers path-style `GET` and `HEAD` of an object from the
content store. It answers a missing key or bucket, and an unsupported operation, with the S3 XML error body.

## Context

- Decision: BL-417's ADR, decisions 1 to 3. Rules: BL-543's ADR (oracle, isolation, copying).
- New projects: `Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary` and `.UnitTests`, listed in
  `Surl.slnx` under `/Protocols/FileTransfer/`. Namespace `Surl.Extended.Protocol.S3`. It references
  `Surl.Protocol.Abstractions`, `Surl.Content` and the HTTP/1.1 library from BL-407, nothing else
  (`ProtocolIsolationTests`, extended by BL-404).
- The server implements `IConnectionProtocolServer` (ADR-0004). Tests replay requests through `InMemoryConnection`.
- Expected bytes come from the S3 API Reference's GetObject and HeadObject pages and "Error responses"
  (`ETag`, `Last-Modified`, `Content-Length`, `x-amz-request-id`, the `<Error><Code>…</Code></Error>` body).
  Never from the port. Authentication is BL-420's, so this task's tests run with anonymous reads allowed.

## Acceptance criteria

- [ ] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [ ] Named tests show:
      - `GET /bucket/key` returns the stored bytes with the headers the ADR lists;
      - `HEAD` returns the same headers and no body;
      - a missing key gets `404 NoSuchKey`;
      - a missing bucket gets `404 NoSuchBucket`;
      - a range request is answered as the ADR says;
      - an unsupported operation gets the ADR's error;
      - an over-limit request is refused as ADR-0006 says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
