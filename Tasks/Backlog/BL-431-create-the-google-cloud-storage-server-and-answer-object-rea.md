---
id: BL-431
title: Create the Google Cloud Storage server and answer object reads, writes and listings
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-429, BL-404, BL-407]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.GoogleCloudStorage.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed:
---
# BL-431 — Create the Google Cloud Storage server and answer object reads, writes and listings

## Goal

`Surl.Extended.Protocol.GoogleCloudStorage.UnitLibrary` exists. It answers XML API GET, HEAD and PUT Object and
List Objects from the content store with bearer authentication, and refuses with the XML API's error bodies.

## Context

- Decision: BL-429's ADR, decisions 1 to 3. Rules: BL-543's ADR.
- New projects in `Protocols/FileTransfer/`, listed in `Surl.slnx`. Namespace
  `Surl.Extended.Protocol.GoogleCloudStorage`. References: `Surl.Protocol.Abstractions`, `Surl.Content`, and
  BL-407's library.
- XML API: object responses carry `x-goog-generation`, `x-goog-hash` (crc32c and md5) and `ETag`; List Objects
  returns `ListBucketResult` with `prefix`, `delimiter`, `marker` and `max-keys`; errors are
  `<Error><Code>…</Code><Message>…</Message></Error>`. CRC32C is computed with `System.Numerics.BitOperations.Crc32C`.
- V4 HMAC is wired in by BL-432. This task's tests use bearer tokens and anonymous reads.

## Acceptance criteria

- [ ] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [ ] Named tests show:
      - GET and HEAD of an object with `x-goog-hash` computed as the API says;
      - a PUT stored under `--allow-uploads` and refused without it;
      - a listing paged with `max-keys` and `marker`;
      - `delimiter=/` prefixes;
      - a missing object gets `404 NoSuchKey`;
      - no token with anonymous off is refused as the ADR says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
