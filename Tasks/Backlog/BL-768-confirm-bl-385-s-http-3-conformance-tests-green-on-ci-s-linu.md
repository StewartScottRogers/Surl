---
id: BL-768
title: Confirm BL-385's HTTP/3 conformance tests green on CI's Linux and macOS legs
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-385]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-768 — Confirm BL-385's HTTP/3 conformance tests green on CI's Linux and macOS legs

## Goal

A `CI` run containing BL-385's commits shows `UpstreamCurlFetchesFromSurlOverHttp3Tests` green on the
Linux and macOS conformance legs, and its URL is recorded here.

## Context

- BL-385 added `Conformance/Surl.Conformance.UnitTests/UpstreamCurlFetchesFromSurlOverHttp3Tests.cs`
  and proved it on Windows with the static 8.21.0 build (ADR-0082 decision 1). Its CI criterion needs
  a pushed commit a dark factory lane may not make, so it moved here, as BL-371's did to BL-724.
- `.github/workflows/ci.yml`'s "Conformance tests" step runs `Surl.Conformance` on all three legs.
  Read runs with `gh run list --workflow CI` and `gh run view <id> --log` (read only).
- On Linux and macOS the tests run the reference builds. `GetWithHttp3Only_Http3Off_FailsToConnect`
  expects exit 7 off Windows, measured on Linux only (ADR-0082 "Measured", build L); macOS was never
  measured (ADR-0082 says BL-385's CI run records build M's ClientHello). If a leg differs, pin that
  platform's measured answer in its own test with `[OSCondition]`, as root `CLAUDE.md` says.

## Acceptance criteria

- [ ] The URL of a `CI` run containing BL-385's commits, with every `UpstreamCurlFetchesFromSurlOverHttp3Tests`
      test passing on the Linux and macOS legs, is in Notes.
- [ ] Any test changed to make a leg green pins that platform's measured answer, and
      `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

Checked 2026-11-06 by the interactive session: in CI run https://github.com/StewartScottRogers/Surl/actions/runs/42291259804
(PR #23, merged, CI "success") all 13 `UpstreamCurlFetchesFromSurlOverHttp3Tests` are **Skipped** (Inconclusive) on
both `ubuntu-latest` and `macos-latest`, as BL-724 found for the 12 HTTP/2 tests in the same run. The cause is
probably shared: investigate the two together.

## Log

- 2026-11-05: Created.
