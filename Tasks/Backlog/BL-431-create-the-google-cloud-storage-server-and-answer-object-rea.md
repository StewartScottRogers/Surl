---
id: BL-431
title: Create the Google Cloud Storage server and answer object reads, writes and listings
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-429, BL-404, BL-407, BL-636, BL-656]
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
- Shared stores (Stewart, 2026-11-04; BL-622's ADR decision 2): objects are files in the one shared
  `ContentStore`. PUT Object's content type, `x-goog-meta-*`, generation and hashes go to BL-636's object metadata,
  not a private store; a file written by another protocol is answered with BL-636's defaults. Every store call
  passes the checked account (or anonymous) and scheme `gs` to BL-656's share access check; a refusal is answered
  as BL-429's ADR names it. Bearer tokens come from the one account set.

## Acceptance criteria

- [ ] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [ ] Named tests show:
      - GET and HEAD of an object with `x-goog-hash` computed as the API says;
      - a PUT stored under `--allow-uploads` and refused without it;
      - a listing paged with `max-keys` and `marker`;
      - `delimiter=/` prefixes;
      - a missing object gets `404 NoSuchKey`;
      - no token with anonymous off is refused as the ADR says;
      - a file put into the content store with no metadata (as FTP writes it) read with BL-636's defaults;
      - a write refused by share access answered with the ADR's error.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-636 and BL-656; uses the shared store's metadata and access check.
