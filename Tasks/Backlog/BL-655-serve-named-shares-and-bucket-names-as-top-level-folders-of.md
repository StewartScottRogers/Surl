---
id: BL-655
title: Serve named shares and bucket names as top-level folders of the content store
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-642, BL-636]
touches: [Foundation/Surl.Content.UnitLibrary, Foundation/Surl.Content.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-655 — Serve named shares and bucket names as top-level folders of the content store

## Goal

`ContentStore` resolves a request's top-level name through configured shares (each a name mapped to its own
path, optionally read-only and limited to some protocols) and bucket names (a bucket mapped onto a content path),
so every file server and object-storage server sees the same shares, and with none configured it serves one root
exactly as today.

## Context

- Decision: BL-622's ADR decision 2 (the seam through which the store resolves a top-level name) and BL-642's ADR
  decision 2 (`--share <name>=<path>[;readonly;protocols=…]`, `--bucket <name>=<path>`, shares without
  `--directory`, how a share's path is checked at start). ADR-0015 (exposure options), ADR-0009 (listings),
  ADR-0018 (a served root that is not a full path), ADR-0031 decision 5 (`.surl` never served, in every share).
- Where: `Foundation/Surl.Content.UnitLibrary` (`ContentStore`, `ContentPathMapping`, `RequestPathSegments`,
  `ContentDirectoryListing`, `IContentFileSystem`). Configuration arrives by constructor; the store learns the
  calling protocol through the API BL-622/BL-642 decide (planning: a scheme argument on each lookup), which
  BL-656 extends with the account.
- A share's path must not escape through `..`, a symlink or a junction any more than the root can (ADR-0002
  decision 2); a share nested inside another share or inside the root is handled as the ADR says.
- The root listing shows the shares (and buckets) as folders, as the ADR says, beside the root's own entries.
- Tests run on `InMemoryContentFileSystem`; platform-neutral paths.

## Acceptance criteria

- [ ] Named tests show: a path under a share's name maps into the share's path; a share with `readonly` refuses an
      upload with the typed outcome the ADR names; a share limited to `ftp,http` is not found from `sftp`; a bucket
      name maps onto its content path; the root listing shows the shares; `..` and `.surl` inside a share are
      refused as at the root; with no share configured every existing `Surl.Content.UnitTests` test passes
      unchanged.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
