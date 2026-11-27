---
id: BL-1800
title: Bring the published-binary conformance platforms and the upstream pass-rate counts up to date in the requirements and the roadmap
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Product/Requirements.md, Documentation/Planning/Roadmap.md]
requirement: NFR-006
created: 2026-11-26
completed:
---
# BL-1800 — Bring the published-binary conformance platforms and the upstream pass-rate counts up to date in the requirements and the roadmap

## Goal

NFR-006, FR-053 and the roadmap name the platforms `release.yml` runs conformance on and the pass
rates the pass-rate file records.

## Context

Found by BL-1798. The `conformance` job in `.github/workflows/release.yml` runs on `linux-x64`,
`osx-arm64`, `linux-arm64`, `win-arm64` and `osx-x64`. NFR-006 in
`Documentation/Product/Requirements.md` still says conformance runs on `linux-x64` and `osx-arm64`
in the dry run and that the other three run the fast tests, and `Documentation/Planning/Roadmap.md`
(about lines 169 to 170) names only those two. FR-053 gives 2026-11-12 counts and lists a pass rate
of 100% as not yet proven, while the "Current" table in
`Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md` shows 100.0% of runnable cases on
`win-x64`, `linux-x64` and `osx-arm64`.

## Acceptance criteria

- [ ] NFR-006 and the roadmap name the same conformance platforms as the `conformance` job's matrix
      in `.github/workflows/release.yml`.
- [ ] FR-053's pass-rate counts and its "not yet proven" list agree with the "Current" table and
      the notes under it in the pass-rate file.

## Notes

## Log

- 2026-11-26: Created.
