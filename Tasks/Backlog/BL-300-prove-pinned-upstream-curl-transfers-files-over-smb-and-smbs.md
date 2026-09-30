---
id: BL-300
title: Prove pinned upstream curl transfers files over smb and smbs with surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-299]
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-050
created: 2026-09-30
completed:
---
# BL-300 — Prove pinned upstream curl transfers files over smb and smbs with surl

## Goal

`[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` prove that pinned upstream curl
8.21.0 downloads and uploads files over `smb` and `smbs` against a live `surl`, in every case BL-283's
ADR lists, on the builds that have SMB: ADR-0030's static-curl Windows build and the Linux and macOS
reference builds.

## Context

- The cases and expected exit codes: BL-283's ADR's list (at least a download, a file over 32 KiB,
  `DOMAIN/user`, an upload with and without `--allow-uploads`, `smbs://` with `-k`, a missing share,
  a missing file, a refused login, and no `-u`, which curl itself refuses with 67).
- Harness: `SurlOnLoopback.cs`, `PinnedUpstreamCurl.cs`, `AccountsFile.cs`,
  `TestCertificateAuthority.cs`. The Windows reference build has no `smb`: locate the build by
  protocol (BL-089's `UpstreamCurlLocator`), and report Inconclusive, naming the pin, where no pinned
  build of the platform lists `smb` - ADR-0026 decision 2's rule, written once beside
  `PinnedUpstreamCurl.RunAsync`; reuse it if an earlier conformance task added it.
- Any disagreement with the pinned build is fixed in the library at fault through a new task filed by
  `task-planner`, never by changing the expected result (ADR-0003); list them in the Log.

## Acceptance criteria

- [ ] Integration tests exist for every case of BL-283's ADR and pass on Windows with the static-curl
      build present: `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [ ] Without an SMB-capable pinned build the tests report Inconclusive with the pin named.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a socket.

## Notes

- Stewart approved lanes filing follow-up tasks beyond the plan (2026-09-30, "yes extra tasks"): a
  disagreement this task finds becomes its own task rather than widening this one.

## Log

- 2026-09-30: Created.
