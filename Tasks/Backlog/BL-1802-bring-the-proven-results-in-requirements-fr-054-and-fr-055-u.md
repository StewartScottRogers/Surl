---
id: BL-1802
title: Bring the proven results in requirements FR-054 and FR-055 up to date and remove the repeated words in FR-057
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1800]
touches: [Documentation/Product/Requirements.md]
requirement: FR-054
created: 2026-11-26
completed:
---
# BL-1802 — Bring the proven results in requirements FR-054 and FR-055 up to date and remove the repeated words in FR-057

## Goal

FR-054 and FR-055 list as proven what has been proven on the Linux and macOS reference builds,
their upstream test-case sentences agree with the pass-rate file, and FR-057 reads without repeated
words.

## Context

Found by BL-1800. All in `Documentation/Product/Requirements.md`; the statements below were verified
on 2026-11-26 and need correcting, not auditing again. It waits on BL-1800, which edits the same file
(FR-053 and NFR-006).

- FR-054 lists under "Not yet proven" "the Linux and macOS reference builds' results, which BL-724
  confirmed green on CI". BL-724 records CI run 42324374116 (head `424106dd`) with the 15
  `UpstreamCurlFetchesFromSurlOverHttp2Tests` tests passing on the Linux and macOS legs.
- FR-055 does the same with BL-768, which records the same run with the 13
  `UpstreamCurlFetchesFromSurlOverHttp3Tests` tests passing on both legs.
- BL-724 and BL-768 are in `Tasks/Done/2026-11-12_1408/`; their Notes hold the evidence. Do not edit
  them.
- The upstream-case sentences of FR-054 and FR-055 name only the cases that run on both `linux-x64`
  and `osx-arm64`. As FR-053 says, and as
  `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md` and the passing lists beside it show,
  the cases needing the Debug curl or the libtests binary (ADR-0173, ADR-0174) also pass on
  `linux-x64` (commit `4ac7b2c7`, CI run 44068385591), and `win-x64` and `osx-arm64` skip them. By
  the server each case file names, those over `http/2` are 358, 359, 2400, 2401, 2402, 2404, 2406
  and 2407, and those over `http/3` are 2501 and 2502.
- FR-057 reads "the harness the harness is BL-577 to BL-579".

No stand-in store, event or fault applies: this task changes prose only.

## Acceptance criteria

- [ ] FR-054 no longer lists the Linux and macOS reference builds' results under "Not yet proven",
      and states that the 15 `UpstreamCurlFetchesFromSurlOverHttp2Tests` tests passed on both in CI
      run 42324374116 (BL-724). Its other unproven item (the cases BL-371 left unmeasured) remains.
- [ ] FR-055 no longer lists the Linux and macOS reference builds' results under "Not yet proven",
      and states that the 13 `UpstreamCurlFetchesFromSurlOverHttp3Tests` tests passed on both in CI
      run 42324374116 (BL-768). Its other unproven items (BL-385's unmeasured cases and the ADR-0147
      groups) remain.
- [ ] FR-054 says 358, 359, 2400, 2401, 2402, 2404, 2406 and 2407 pass on `linux-x64` with the Debug
      curl or libtests binary and that `win-x64` and `osx-arm64` skip them.
- [ ] FR-055 says 2501 and 2502 pass on `linux-x64` with the Debug curl or libtests binary and that
      `win-x64` and `osx-arm64` skip them.
- [ ] FR-057 contains "the harness is BL-577 to BL-579" and no "the harness the harness".

## Notes

## Log

- 2026-11-26: Created.
