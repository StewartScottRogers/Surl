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
completed: 2026-11-05
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

- [x] Named tests show: a path under a share's name maps into the share's path; a share with `readonly` refuses an
      upload with the typed outcome the ADR names; a share limited to `ftp,http` is not found from `sftp`; a bucket
      name maps onto its content path; the root listing shows the shares; `..` and `.surl` inside a share are
      refused as at the root; with no share configured every existing `Surl.Content.UnitTests` test passes
      unchanged.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

- Tests (`ContentStoreTests.Shares.cs`, `ContentShareTests`, `ContentBucketTests`):
  `MapRequestPath_PathUnderAShareName_MapsIntoTheSharesPath`,
  `EveryWrite_InAReadOnlyShare_IsNotPermittedWhateverAllowUploadsSays` (`ContentUploadResult.NotPermitted`, ADR-0102
  2.3), `AShareLimitedToFtpAndHttp_IsMissingFromAnotherScheme` (sftp), `MapBucketRequestPath_ABucketName_MapsOntoItsContentPath`,
  `ListDirectory_TheRoot_ShowsEachVisibleShareInPlaceOfItsNamesake`,
  `MapRequestPath_DotDotOrASeparatorInsideAShare_IsRefusedAsAtTheRoot`,
  `TheServiceStateFolderInsideAShare_IsNeverServed`, `MapRequestPath_ASymbolicLinkOutOfAShare_IsRefusedAsOneOutOfTheRoot`.
  All 488 existing tests pass unchanged; the library is at 100% line and branch coverage (cobertura, fast tests).
- Implementation choices (sensible defaults, within ADR-0102; no new ADR because `Documentation/Planning/Decisions`
  is held by BL-612 and ADR-0102 already decides the behaviour):
  - ADR-0100 2.6's planning name `IContentShareLookup` became no interface: the store takes `ContentShare` and
    `ContentBucket` lists by a new constructor, and each `ContentPathMapping` carries its container (internal
    `ContentContainer`: root, file system, read-only), so a share on disk sits beside an in-memory root and every
    existing operation runs against the right file system. The three-argument constructor is unchanged.
  - The scheme lookup is `MapRequestPathForScheme(path, scheme)`, not an overload of `MapRequestPath`: an overload
    made SMB's unqualified `cref` ambiguous (CS0419), and SMB is outside this task. `MapRequestPath(path)` names no
    scheme and sees only shares open to every scheme, so a `protocols=` share stays hidden from servers that do not
    yet pass their scheme (BL-657 to BL-662). Schemes compare ignoring case; the composition (BL-663) lists a scheme's
    TLS twin in `Schemes` itself (ADR-0102 decision 4.3).
  - A share invisible to the scheme is answered as absent and still shadows the root entry of its name; share names
    match ordinally.
  - A rename between the root and a share, or between two shares, is `NotPermitted` (they may be on different file
    systems and carry different rules); the share's own directory can be neither renamed nor removed.
  - Overlap between shares and `--directory` needs the disk and is left to the composition at start (BL-663); the
    store refuses only duplicate share or bucket names and invalid names (`.surl`, any segment a request may not hold).
  - Only `MapBucketRequestPath` reads bucket names. `ObjectMetadataIndex` still keys by path relative to the served
    root; how object servers key files inside a share is for their "serve from the executable" tasks.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-05: Doing -> Done. ContentStore serves named shares (own path and file system, readonly, protocols=) and bucket names as top-level folders; one root as before when none is configured
