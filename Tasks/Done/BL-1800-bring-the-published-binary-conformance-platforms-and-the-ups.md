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
completed: 2026-11-26
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

- [x] NFR-006 and the roadmap name the same conformance platforms as the `conformance` job's matrix
      in `.github/workflows/release.yml`.
- [x] FR-053's pass-rate counts and its "not yet proven" list agree with the "Current" table and
      the notes under it in the pass-rate file.

## Notes

- Platforms: NFR-006 and the roadmap now name `linux-x64`, `linux-arm64`, `win-arm64`, `osx-x64`
  and `osx-arm64`, with `win-x64` conformance run locally.
- Pass rates: FR-053 gives the "Current" table's three rows with their commits and dates. All ten
  Debug-curl and libtest HTTP/2 and HTTP/3 cases were checked to be in
  `upstream-test-cases-passing.linux-x64.txt`. The roadmap's Phase 6 text says the same.
- `dotnet build` clean (0 warnings) and the fast tests green. No `.cs` or project file changed.
- Follow-ups filed: BL-1801 (glossary, `Surl.Conformance` CLAUDE.md, pass-rate file notes) and
  BL-1802 (FR-054, FR-055, FR-057). README and product overview statements are BL-1799's.
- Not filed: this branch's "Releases" table in the pass-rate file stops at `0.1.2`; the report from
  `align-and-document` is that `origin/master` has rows through `0.1.11` (not re-checked here).

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. NFR-006, FR-053 and the roadmap now name release.yml's five conformance platforms and the pass-rate file's current counts
