---
id: BL-1805
title: Correct FR-054's AWS SigV4 HTTP/2 proof and FR-057's libcurl-row task and intro statements in the requirements
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1804]
touches: [Documentation/Product/Requirements.md]
requirement: FR-054
created: 2026-11-26
completed:
---
# BL-1805 — Correct FR-054's AWS SigV4 HTTP/2 proof and FR-057's libcurl-row task and intro statements in the requirements

## Goal

FR-054 states what is proven for its two AWS SigV4 HTTP/2 tests on Linux and macOS, and FR-057 and
the intro paragraph that describes it agree with ADR-0089 and the FR-057 row.

## Context

Found during BL-1804, all in `Documentation/Product/Requirements.md`. Checked on 2026-11-26.

- FR-054 counts 17 tests in `UpstreamCurlFetchesFromSurlOverHttp2Tests` and says 15 passed on the
  Linux and macOS reference builds at head `424106dd` (CI run 42324374116). Commit `1b4d7c853`
  (BL-1549) added `GetWithAwsSigV4_HttpsByAlpn_IsServedOverHttp2` and
  `GetWithAwsSigV4WrongSecret_HttpsByAlpn_ExitsHttpReturnedError`. The row names no CI run for them,
  and "Not yet proven" does not mention them. BL-1549's Notes record the class passing 17 of 17 on
  the Windows static 8.21.0 build.
- Whether a CI run covered the two tests on Linux and macOS is not checked. `origin/master` contains
  `1b4d7c853`, and `ci.yml` has successful runs from 2026-11-21 on. Find one with
  `gh run list --workflow ci.yml --status success`, confirm
  `git merge-base --is-ancestor 1b4d7c853 <headSha>`, and read `gh run view <id> --log` (read only).
- A green run alone is not proof: an Inconclusive test counts as skipped. The "Conformance tests"
  step's console log names skipped and failed tests, not passed ones. On 2026-11-26 these two method
  names occurred only in `UpstreamCurlFetchesFromSurlOverHttp2Tests`, so no other class's skip line
  can be taken for theirs. BL-724's Notes (`Tasks/Done/2026-11-12_1408/`) show how the 15 were read.
- FR-057 says "the measurements are BL-580 to BL-591" and lists in that parenthesis the libcurl rows
  (`CurlPortLibcurlBesideUpstreamLibcurlOver*Tests`). ADR-0089 decision 1(c) and its header give the
  libcurl rows to BL-597, and say BL-580 to BL-590 are one family each and BL-591 the upstream test
  cases. BL-597 was split into BL-934, BL-935, BL-936, BL-937 and BL-942, all Done.
- The paragraph under the requirements table that begins "FR-049 to FR-051 end with" says FR-057 "is
  built for `win-x64` and ends with what is not yet measured". The FR-057 row says it is built for
  `win-x64`, `linux-x64` and `osx-arm64` and states what is not measurable. The same paragraph says
  "derived from Stewarts approval", missing an apostrophe.

No stand-in store, event or fault applies: this task changes prose only. No test is changed and no
workflow is dispatched unless no existing run answers the question (the skill's "Waiting on a
workflow run").

## Acceptance criteria

- [ ] FR-054 either names a CI run ID and head commit containing `1b4d7c853` in which both AWS SigV4
      tests passed on the Linux and macOS reference builds, or lists under "Not yet proven" each
      platform on which no run shows them passing. This task's Notes record how that was read.
- [ ] FR-057 attributes the libcurl rows to BL-597, outside the "BL-580 to BL-591" measurements.
- [ ] The paragraph under the table says FR-057 is built for `win-x64`, `linux-x64` and `osx-arm64`,
      and that its row states what is not measurable, as the FR-057 row does.
- [ ] `Documentation/Product/Requirements.md` contains no "Stewarts".

## Notes

## Log

- 2026-11-26: Created.
