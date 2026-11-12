---
id: BL-865
title: Cache the OCI registry's blob digest checks in the object metadata index
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-436, BL-1035]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests, Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-865 — Cache the OCI registry's blob digest checks in the object metadata index

## Goal

The OCI registry hashes a blob or manifest file once per file stamp (size and last write time) and caches the
result in `ObjectMetadataIndex`, instead of hashing the whole file on every pull.

## Context

- ADR-0115 decision 2.1: "the check runs on the first read and its result is cached in ADR-0100's object
  metadata index against the file's stamp (size and last-write time), so a file replaced by another protocol is
  checked again".
- BL-436 built the check in `OciRepository` (`ReadVerifiedAsync`, `HashMatchesAsync`) without the cache: every
  read hashes the whole file, which is correct but reads a large layer twice before it is served.
- `ObjectMetadataIndex` (`Foundation/Surl.Content.UnitLibrary`) already stamps entries and drops them when the
  file changes. The server needs the index passed to `OciRegistryProtocolServer`, as `S3ProtocolServer` takes it.
  If recording a verified digest needs a new `ObjectMetadata` field, that is a `Surl.Content` change: add it to
  `touches` by the board's rules.

## Acceptance criteria

- [x] A named test shows a blob pulled twice is hashed once (a counting `UnitTest…ContentFileSystem` fake sees
      one whole-file read before the serve), and a file replaced through the store is hashed again.
- [x] A tampered file is still `404 BLOB_UNKNOWN` or `MANIFEST_UNKNOWN`, and noted.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- 2026-11-11 (lane 3): the code is written and verified, but left uncommitted for the shift to stash. The
  pinned SDK 10.0.401 is not installed (BL-1035, Stewart), so the build gate cannot be checked as written. With
  `global.json` rolled forward to SDK 11.0.100 in the worktree only, then restored: `dotnet build -warnaserror`
  was clean on the whole solution, every fast test passed, and Surl.Content and the OCI registry were at 100%
  line and 100% branch coverage. Next run: rebuild on the pinned SDK, tick the last box, commit.
- `touches` now includes Foundation/Surl.Content.UnitLibrary and .UnitTests: recording a verified digest without
  `ReadAsync`'s MD5 ETag (a second whole-file read) needs a new index API. No task in Doing named them.
- Design (decided under the delegation, by ADR-0115 decision 2.1): `ObjectMetadataIndex` gains
  `RecordVerifiedDigestAsync(key, digest, hashedStatus)` and `IsDigestVerifiedAsync(key, digest)`, held
  beside the entries in memory only, at most `MaxEntries` of them, forgotten on reset or restore. Not
  persisted, so the index file format (`SURL-OBJECT-INDEX-2`) stays the same and S3 sees no new user
  metadata or ETag. The cost is one hash per file after a restart. A record is made only when the file's
  stamp still equals the status read before hashing, so a file replaced mid-hash is never marked verified.
- `OciRegistryProtocolServer` gains a public `(store, index, policy, timeProvider)` constructor, as
  `S3ProtocolServer` has. The 3-argument one stays and hashes on every read, so Surl.Composition (BL-1028's
  lane) is not broken. Repositories in a share are not cached, because the index keys only the served root.
- Known limit, as with every stamp-keyed entry: under a frozen `TimeProvider`, a same-length file written
  directly into the file system keeps its stamp and is not checked again (`OciRepositoryTests`
  `ServeAsync_BlobInAShare_...` shows the uncached case refusing it).
- Also covered an existing gap: `OciTokenEndpoint` with no query string
  (`AnswerAsync_NoQueryAtAll_IssuesATokenForNoScope`).
- Tests: `OciRepositoryTests` (7, including `ServeAsync_BlobPulledTwice_IsHashedOnceAndAgainOnceReplacedThroughTheStore`
  and the tamper tests), `UnitTestCountingContentFileSystem`, and 6 new `ObjectMetadataIndexTests`.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Code done and verified on rolled-forward SDK 11; waiting on BL-1035 (pinned .NET SDK 10.0.401 not installed) to pass the build gate
- 2026-11-11: Backlog -> Doing.
- 2026-11-12: Doing -> Backlog. returned by the shift starting 20261112-052315: it was in Doing but no stopped lane held it; look for its work in 'git stash list' and in factory/BL-865-* branches before redoing it
