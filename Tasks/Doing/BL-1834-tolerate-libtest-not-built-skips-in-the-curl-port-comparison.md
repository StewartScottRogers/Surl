---
id: BL-1834
title: Tolerate libtest-not-built skips in the Curl port comparison and correct the conformance runner's libtest wording
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: FR-053
created: 2026-11-26
completed:
---
# BL-1834 — Tolerate libtest-not-built skips in the Curl port comparison and correct the conformance runner's libtest wording

## Goal

The Curl port comparison drops a listed case skipped `libtest-not-built:<program>` in a run with no
`libtests` binary, and the runner's comments and refusal message about libtest say what the code does.

## Context

Found during BL-1808 (2026-11-26) by reading the code; nothing was run.

- `UpstreamTestCasesAgainstSurlTests.ToleratedSkipReasons` adds `libtest-not-built`.
  `UpstreamTestCasePassingListComparison` compares the reason's text before any colon, but
  `CurlPortBesideUpstreamCurlOverUpstreamTestCasesTests` (in `Surl.Conformance.UnitTests`) matches
  the whole reason with `skippedForWantOfABuild.Contains(row.Upstream.Reason)`, so a
  `libtest-not-built:lib2402` row is kept. With the Debug build present and no `libtests` file, a
  listed libtest case would then likely be judged a port defect.
- The XML comment on `ToleratedSkipReasons` names `libtest` but not `libtest-not-built`.
- The XML comment on `RunBesideAsync` says upstream's side uses the reference or smb build; the
  Debug and `libtests` clients apply too.
- `UpstreamCurlRunner`'s constructor calls every pin whose kind is not `Curl` "a pinned upstream
  libcurl", including a `Libtest` one.

Stand-in questions: none of this changes a server, store, event or fault; it is test tooling only.

## Acceptance criteria

- [ ] One filter in `Surl.Conformance.UnitLibrary` or `Surl.Conformance.UnitTests` decides whether a
      skip reason is tolerated, comparing the text before any colon, and both the passing-list guard
      and the Curl port comparison use it.
- [ ] A named fast test shows a row skipped `libtest-not-built:lib2402` is dropped when
      `libtest-not-built` is tolerated and kept when it is not.
- [ ] The XML comments on `ToleratedSkipReasons` and `RunBesideAsync` name `libtest-not-built` and
      the Debug and `libtests` clients respectively.
- [ ] `UpstreamCurlRunner` refusing a `Libtest` pin does not call it a libcurl, shown by a named
      test, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-27: Backlog -> Doing.
