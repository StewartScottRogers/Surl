---
id: BL-656
title: Check each account's read and write access to a share and each protocol's reach in the content store
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-655]
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-656 — Check each account's read and write access to a share and each protocol's reach in the content store

## Goal

`ContentStore` decides, for the logged-in account (or anonymous) and the calling protocol, whether a read, a
listing or a write in a share is allowed by the configured share access, and refuses with one typed outcome every
server can answer in its own words; with no share access configured it allows exactly what it allows today.

## Context

- Decision: BL-642's ADR decision 2 (`--share-access <name>=<user>:<rw|r>,...`, the default for an unlisted
  account once any access is configured, how `--allow-uploads` and `--allow-anonymous` combine with it).
  Group members arrive already expanded (BL-646).
- Where: `Foundation/Surl.Content.UnitLibrary`, extending BL-655's lookup with the caller (planning: a small
  caller record with account name or anonymous, and scheme). Every store operation that reads, lists, uploads,
  renames or deletes takes it.
- The refusal must not tell a peer whether a path exists in a share it may not read (ADR-0006 section 3, ADR-0032
  section 8's principle): the ADR says whether "no access" and "not found" are one outcome.
- Each file server passes its caller in its own task (BL-657 to BL-662); the object-storage servers in theirs
  (BL-419, BL-425, BL-431, BL-437). Until a server passes a caller, it calls an overload the ADR names that behaves
  as today, so this task breaks no server.

## Acceptance criteria

- [ ] Named tests show: an account with `r` reads and lists but cannot upload, rename or delete; `rw` can; an
      unlisted account and anonymous get the ADR's default; `--allow-uploads` off still refuses every write; the
      refusal outcome is the same for a missing path and a forbidden one if the ADR says so; with no access
      configured every existing test passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
