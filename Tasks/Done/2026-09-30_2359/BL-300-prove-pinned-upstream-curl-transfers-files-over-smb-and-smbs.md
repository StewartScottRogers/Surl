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
completed: 2026-09-30
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

- [x] Integration tests exist for every case of BL-283's ADR and pass on Windows with the static-curl
      build present: `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [x] Without an SMB-capable pinned build the tests report Inconclusive with the pin named.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a socket.

## Notes

- Stewart approved lanes filing follow-up tasks beyond the plan (2026-09-30, "yes extra tasks"): a
  disagreement this task finds becomes its own task rather than widening this one.

- `UpstreamCurlTransfersFilesWithSurlOverSmbTests` (24 cases) covers every row of ADR-0073 decision 11.
  On Windows it ran the static-curl build 589C8E4D… and every case passed with the ADR's exit code.
- ADR-0026 decision 2's rule now lives once, as `PinnedUpstreamCurl.RunForProtocolAsync`. It locates the
  build with `UpstreamCurlLocator.LocateForProtocol`, so on Windows it skips the reference build, which has
  no `smb`. Where no pinned build lists the protocol, or the pinned file is absent, it calls
  `Assert.Inconclusive` with the locator's message, which names the platform and protocol or the pin's path.
- Choice: curl runs with `-m 20` so that a hang (ADR-0073 rows 24 to 27) shows up as curl's 28 inside the
  runner's 30-second limit, not as a runner timeout.
- `smbs` cases use the same SMB-capable build: every pin that lists `smb` also lists `smbs`.
- The pinned build disagreed with nothing, so no follow-up tasks were filed.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Pinned upstream curl 8.21.0 downloads and uploads over smb and smbs against surl in every ADR-0073 decision 11 case
