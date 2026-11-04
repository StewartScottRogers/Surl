---
id: BL-360
title: Prove the published surl on all six runtime identifiers in CI
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-358, BL-359]
touches: [.github/workflows/release.yml, .github/workflows/ci.yml]
requirement: none
created: 2026-11-04
completed:
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

- [ ] Every runtime identifier, `osx-x64` included, runs the ADR's smoke test on the runner the ADR
      names. The stale "until Phase 1 lands" comment and the acceptance of exit 2 are gone.
- [ ] On `win-x64`, `linux-x64` and `osx-arm64`, `Surl.Conformance` runs against the published binary,
      with that platform's pinned reference build, wherever the ADR places the run.
- [ ] A `workflow_dispatch` run of `release.yml` on the branch is green on all six runtime
      identifiers, and Notes record its run URL.
- [ ] If any package name changed, `DOWNLOAD.md`, `install.sh` and `install.ps1` change in the same
      diff. If none changed, those three files are untouched.

## Notes

## Log

- 2026-11-04: Created.
