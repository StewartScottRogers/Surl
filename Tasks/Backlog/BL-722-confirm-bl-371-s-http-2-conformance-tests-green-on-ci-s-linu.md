---
id: BL-722
title: Confirm BL-371's HTTP/2 conformance tests green on CI's Linux and macOS legs
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-371]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-722 — Confirm BL-371's HTTP/2 conformance tests green on CI's Linux and macOS legs

## Goal

A `CI` run containing BL-371's commits shows `UpstreamCurlFetchesFromSurlOverHttp2Tests` green on the
Linux and macOS conformance legs, and its URL is recorded here.

## Context

- BL-371 added `Conformance/Surl.Conformance.UnitTests/UpstreamCurlFetchesFromSurlOverHttp2Tests.cs`
  and proved it on Windows with the static 8.21.0 build (ADR-0081 decision 1). Its criterion 3 (CI's
  Linux and macOS legs) needs a pushed commit a dark factory lane may not make, so it moved here, as
  BL-028's did to BL-079.
- `.github/workflows/ci.yml`'s "Conformance tests" step runs `Surl.Conformance` on all three legs.
  Read runs with `gh run list --workflow CI` and `gh run view <id> --log` (read only); the shift
  branch's merge pull request runs CI with these commits.
- On Linux and macOS the tests run the reference builds. If a leg differs from Windows (for example
  curl's `-v` wording for connection reuse), pin that platform's answer in its own test with
  `[OSCondition]`, as root `CLAUDE.md` says.

## Acceptance criteria

- [ ] The URL of a `CI` run containing BL-371's commits, with every `UpstreamCurlFetchesFromSurlOverHttp2Tests`
      test passing on the Linux and macOS legs, is in Notes.
- [ ] Any test changed to make a leg green pins that platform's measured answer, and
      `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-05: Created.
