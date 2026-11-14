---
id: BL-1128
title: Substitute %H2CVER with h2c in the upstream test-case run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1128 — Substitute %H2CVER with h2c in the upstream test-case run

## Goal

The upstream test-case run gives the variable `H2CVER` the value `h2c`, as upstream's runtests
does, so upstream test 1800 ("HTTP/2 upgrade refused") passes on CI's Linux and macOS legs.

## Context

- CI run 43629075978 (head `356da7cd`, curl 8.21.0 reference pins): 1800 fails `protocol` on Linux
  and macOS. Its `<protocol>` expects `Upgrade: %H2CVER`. `H2CVER` is in
  `UpstreamTestCaseVariables.Names` (`Conformance/Surl.Conformance.UnitLibrary`), but
  `RunEnvironment` in `Conformance/Surl.Conformance.UnitTests/UpstreamTestCasesAgainstSurlTests.cs`
  never sets it, so the expected line is not what curl sends.
- Upstream: `tests/servers.pm` at tag `curl-8_21_0` holds
  `my $h2cver = "h2c";          # this version is decided by the nghttp2 lib being used` (read
  2026-11-14). That is the value.
- Put the value where a fast test can read it, as a public constant or member of
  `UpstreamTestCaseVariables` (for example `H2cVersion`), with an XML doc comment citing
  `servers.pm`, and use it in `RunEnvironment`.
- The Windows reference build has no HTTP2, so 1800 skips `feature-missing:h2c` there. The only other
  case naming `%H2CVER`, 1801, is `disabled-upstream`.
- 1800's expected `HTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA` is what curl sends, not Surl; if 1800
  still fails on CI after this, that is the recording task's finding, not this task's.
- Stand-in: not applicable. This is conformance tooling.

## Acceptance criteria

- [ ] A fast test preprocesses test 1800 with the run's variables and finds the line
      `Upgrade: h2c` in its `<protocol>` part.
- [ ] `RunEnvironment` sets `H2CVER` from the new member; no literal `"h2c"` is repeated in the test
      class.
- [ ] `dotnet build -warnaserror` is clean for both Conformance projects, the fast tests are green,
      and `Surl.Conformance.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-14: Created.
