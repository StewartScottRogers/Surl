---
id: BL-360
title: Prove the published surl on all six runtime identifiers in CI
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-358, BL-359]
touches: [.github/workflows/release.yml, .github/workflows/ci.yml, .github/actions, Test-PublishedSurl.ps1]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-360 — Prove the published surl on all six runtime identifiers in CI

## Goal

CI runs BL-358's checks against the published native `surl` on all six runtime identifiers, and a
`workflow_dispatch` run of `release.yml` on the branch is green on every one of them.

## Context

- Decision: BL-358's ADR. It settles the smoke test, the runner for `osx-x64`, which runtime
  identifiers run `Surl.Conformance` against the published binary, and which checks run on pull
  requests versus only in the release dry run. Where the ADR differs from anything below, follow the
  ADR.
- `.github/workflows/release.yml`: its smoke test only runs `surl --version`, accepts exit 2 under a
  stale comment ("until Phase 1 lands it exits 2"), and skips `osx-x64`. Its header comment says that
  `DOWNLOAD.md`, `install.sh` and `install.ps1` depend on its package names. Keep those names unless
  the ADR changes them.
- `.github/workflows/ci.yml` obtains and verifies the pinned upstream curl on `windows-latest`,
  `ubuntu-latest` and `macos-latest`, then runs `--filter "FullyQualifiedName~Surl.Conformance"`.
  BL-359 lets that suite run against a published binary.

## Acceptance criteria

- [x] Every runtime identifier, `osx-x64` included, runs the ADR's smoke test on the runner the ADR
      names. The stale "until Phase 1 lands" comment and the acceptance of exit 2 are gone.
- [x] On `win-x64`, `linux-x64` and `osx-arm64`, `Surl.Conformance` runs against the published binary,
      with that platform's pinned reference build, wherever the ADR places the run.
- Moved to BL-603 (see Notes): a `workflow_dispatch` run of `release.yml` green on all six runtime
  identifiers, with its run URL.
- [x] If any package name changed, `DOWNLOAD.md`, `install.sh` and `install.ps1` change in the same
      diff. If none changed, those three files are untouched.

## Notes

- Touches widened (rule 3): ADR-0085 puts the smoke test in `Test-PublishedSurl.ps1` at the
  repository root and the shared obtain-and-verify steps in a composite action under
  `.github/actions/`; no task in Doing names either, so both are added.
- Delivered: `Test-PublishedSurl.ps1` (ADR-0085 decision 1, all four checks, the scrubbed
  environment, the `ubuntu:22.04` container rerun on Linux, `ldd`/`otool -L`/PE import checks);
  `.github/actions/upstream-curl/action.yml` (ci.yml's pin, cache, download, verify, OpenLDAP and
  kinit steps, moved verbatim with `matrix.*` turned into inputs); `ci.yml` uses it, otherwise
  unchanged (decision 5); `release.yml` runs the smoke test on all six legs, `osx-x64` on
  `macos-15-intel`, the fast tests on `win-arm64`, `linux-arm64`, `osx-x64`, a `conformance` job
  for `linux-x64` and `osx-arm64` with `SURL_EXECUTABLE`, the widened paths and a Monday 05:00 UTC
  schedule; the release job now needs `conformance` too and downloads only `surl-*` artifacts.
- Choice: `dotnet publish` in `release.yml` gains `-p:PublishDocumentationFile=false
  -p:PublishReferencesDocumentationFiles=false -p:AllowedReferenceRelatedFileExtensions=.none`.
  Without them the publish directory also holds every referenced library's `.pdb` and `.xml`, which
  decision 1's first check rejects; with them it holds `surl.exe` and `surl.pdb` only (measured on
  `win-x64`). The package contents and names are unchanged, so `DOWNLOAD.md`, `install.sh` and
  `install.ps1` are untouched.
- Choice: the smoke test's request is the plain GET the pinned 8.21.0 reference build sent in
  `Foundation/Surl.Authentication.UnitTests/Fixtures/digest-non-ascii/request.bin`, path and port
  substituted.
- Verified locally on `win-x64` (native publish, version `0.0.0-dev.7`): all four checks pass; a
  wrong `-Version` fails check 2 and an added `surl.runtimeconfig.json` fails check 1, each naming
  the check; the PE import reader lists `KERNEL32.dll`, `ws2_32`, `bcrypt` and the like, no
  `hostfxr`/`coreclr`. Not verifiable here: the Linux container leg (Docker daemon not running on
  this machine) and the macOS legs.
- `win-x64` conformance against the published binary (ADR decision 2's local run): 986 passed,
  5 failed, 59 skipped of 1050 on the first full run; the failures differ run to run, are all
  30 s start timeouts, and pass when run alone in both modes. Filed as BL-604 (it lives in
  `Conformance/`, which BL-391 in Doing touches).
- Criterion 3 moved to BL-603: a lane may not push, so it cannot dispatch a run of its own commits;
  BL-603 depends on BL-360 and dispatches `release.yml` once the shift has integrated them.
- Fast tests: all green (e.g. Surl.Conformance.UnitTests 564, Surl.Console.UnitTests 422); build clean.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. release.yml smoke-tests the native surl on all six runtime identifiers and runs conformance against the published linux-x64 and osx-arm64 binaries; dispatch run in BL-603
