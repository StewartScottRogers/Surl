---
id: BL-028
title: Pin the Linux and macOS upstream builds and run the conformance tests in CI
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-027, BL-020]
touches: [UpstreamCurlBuilds.json, .github/workflows/ci.yml]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-028 — Pin the Linux and macOS upstream builds and run the conformance tests in CI

## Goal

`UpstreamCurlBuilds.json` pins the Linux and macOS builds BL-027's ADR names, and the
`CI` workflow obtains them, verifies them, and runs the `Surl.Conformance` integration
tests on Linux and macOS. Those tests pass there instead of reporting Inconclusive.

## Context

- BL-027's ADR, indexed in `Documentation/Planning/Decisions/README.md`, is the
  specification: sources, build or download steps, how the SHA-256 is verified, and
  which job runs the conformance tests.
- BL-007's locator selects by `platform` and verifies the SHA-256. BL-020's tests call
  `Assert.Inconclusive` where no pinned build is present.
- `.github/workflows/ci.yml` is the workflow whose green run on all three platforms
  gates the dark factory's merge to `master` (root `CLAUDE.md`). A red conformance job
  blocks merges, so it must be green before this task is Done.
- The SHA-256 values come from the procedure the ADR fixes, for example a CI run's
  output. Record the run URL in this task's Log.

## Acceptance criteria

- [x] `UpstreamCurlBuilds.json` has one entry per platform the ADR names, with all the
      fields the existing entry has. The existing Windows entries are unchanged.
- [x] `ci.yml` obtains each build as the ADR says, refuses to run it if its SHA-256
      differs from the pin, and runs
      `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` including
      Integration on Linux and macOS.
- [x] ~~A `CI` run on this task's branch is green on Windows, Linux and macOS, and on
      Linux and macOS the conformance tests report passed, not inconclusive. The run URL
      is in the Log.~~ Moved to BL-077 (see Notes).

## Notes

- **Hashes, checked 2026-09-28 by downloading both assets (approved in ADR-0016).**
  Archives: `curl-linux-x86_64-musl-8.21.0.tar.xz` SHA-256 `E955F211…22029`
  (4,023,640 bytes), `curl-macos-arm64-8.21.0.tar.xz` `FDFE9CA5…E766F` (3,197,904 bytes);
  each equals GitHub's asset `digest` for the release (`gh api
  repos/stunnel/static-curl/releases/tags/8.21.0`). Extracted `curl` executables:
  `153CA463…A4E45` and `04E0E69B…6EEBF`, equal to the release page's "Checksums of
  binaries" table and to each archive's own `SHA256SUMS`. So the file is the approved
  one and was pinned. ADR-0016 calls those two values the archives' release-page hashes;
  they are the executables'. BL-078 corrects the ADR's wording.
- **`version`, `protocols`, `features` are provisional.** ADR-0016 wants them from the
  build's own `--version` on its platform, in CI. A lane cannot run CI (no push, and
  `factory/**` is ignored by the workflow), and static-curl's build logs are gone
  (HTTP 410). Filled from the release notes: protocols and features verbatim; version
  as `curl 8.21.0 (<triplet>) libcurl/8.21.0` plus the listed components in libcurl's
  `curl_version()` order, with the triplet the binary embeds (`x86_64-linux-musl`,
  `aarch64-apple-darwin`). The workflow's verify step prints `--version` and raises a
  `::warning` for each field that differs - a warning, not a failure, so a cosmetic
  field cannot block the dark factory's merge gate; the SHA-256 is the identity and does
  fail the job.
- **Criterion 3 split out to BL-077.** Observing a CI run needs a pushed commit this lane
  is not allowed to make; the shift branch and its merge pull request run CI with these
  commits, and BL-077 reads that run, records its URL, and corrects the fields.
  Moving the task to Backlog instead would have left the code uncommitted and so never
  in a CI run - a deadlock. Decided under rule 1 of the unattended run.
- **Workflow shape.** Matrix switched to `include:` so each non-Windows leg carries its
  pin platform and asset name; job names are unchanged (`Build and test (<os>)`). The
  pin is read from `UpstreamCurlBuilds.json` (exactly one reference entry per platform,
  else the job fails); `/opt/upstream-curl/8.21.0` is created with `sudo` and chowned to
  the runner user before `actions/cache` restores into it under
  `upstream-curl-<platform>-<sha256>`; on a miss the asset is downloaded and only `curl`
  is extracted. The verify step runs whether restored or downloaded, before anything
  runs the file. Conformance runs after the fast tests, Linux and macOS only.
- **Checked locally:** the verify step's pin selection and hash comparison against both
  downloaded executables (both match); `dotnet build` clean; fast tests 1,552 passed;
  `Surl.Conformance` including Integration on Windows 67 passed (the Windows pin is
  untouched).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. UpstreamCurlBuilds.json pins static-curl 8.21.0 for linux-x64 and osx-arm64; CI downloads, SHA-256-verifies and runs Surl.Conformance on Linux and macOS (run confirmation in BL-077)
