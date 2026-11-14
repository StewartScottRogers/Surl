---
id: BL-1127
title: Pass a case's non-ASCII command-line bytes to curl unchanged on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1127 — Pass a case's non-ASCII command-line bytes to curl unchanged on Linux and macOS

## Goal

On Linux and macOS, curl receives a case's command-line arguments and `<setenv>` values as the
exact bytes the case file holds (runtests hands them to a shell as bytes), not a UTF-8 re-encoding
of their Latin-1 reading. Upstream tests 165, 469, 955, 956, 957, 958, 962, 963, 964, 965, 966,
967, 968, 1448, 2046 and 2047 then pass on CI's Linux and macOS legs.

## Context

- CI run 43629075978 (head `356da7cd`, reference pins `linux-x64` and `osx-arm64`, curl 8.21.0):
  all 16 cases above fail on both legs (`protocol` for 165 and 955-968, `stdout` for 1448, 2046 and
  2047, `stderr` for 469). They are exactly the runnable cases whose `<command>` holds non-ASCII
  bytes (`%hex[%c3%a5%c3%a4%c3%b6]hex%` and the like); every such case fails, and none passes. On
  Windows they skip: 165, 1448, 2046 and 2047 `disabled-upstream` (upstream's `DISABLED` list), the
  rest `feature-missing:!win32` (local reference run, 2026-11-14).
- Cause, read from the code: `UpstreamTestCasePreprocessor` decodes the case as Latin-1, so the
  bytes `C3 A5` become the two characters `U+00C3 U+00A5`; `UpstreamCurlRunner` adds each argument
  to `ProcessStartInfo.ArgumentList`, which .NET encodes as UTF-8 on Unix, so curl receives
  `C3 83 C2 A5`. Test 955 then sees `MAIL FROM:<AvsÃ¤ndaren...>` and 165's IDN host is not the one
  the case names.
- Fix in `Conformance/Surl.Conformance.UnitLibrary` (where `UpstreamTestCaseCommandLine` builds the
  arguments and the `<setenv>` environment): on a non-Windows platform, take each argument's and
  each environment value's Latin-1 bytes and, when they are valid UTF-8, pass the string those bytes
  decode to, so .NET's UTF-8 encoding gives back the case's bytes. A value whose bytes are not valid
  UTF-8 stays as it is today. On Windows leave arguments as they are: the Windows reference build has
  no `Unicode` feature and reads its arguments in the ANSI code page, which matches today's Latin-1
  reading for these bytes. Make the platform a parameter so both branches have fast tests.
- Stdout, stderr and the protocol log are already compared as bytes (stderr is decoded and
  re-encoded as UTF-8 by `UpstreamCurlRunner` and `UpstreamTestCaseRunRecord`), so no verify-side
  change should be needed. Check 469's `<stderr>` against that.
- The lane cannot run these cases (Windows skips them). Prove it with fast tests. CI's Linux and
  macOS legs prove the cases in the recording task that depends on this one.
- Stand-in: not applicable. This is conformance tooling; no protocol server changes.

## Acceptance criteria

- [ ] A fast test builds test 955's command line for a non-Windows platform, and the UTF-8 encoding
      of its `--mail-from` argument is the bytes `41 76 73 C3 A4 6E 64 61 72 65 6E 40 ...`, the case
      file's bytes.
- [ ] A fast test builds test 165's command line for Windows and gets today's arguments, unchanged.
- [ ] A fast test shows an argument whose Latin-1 bytes are not valid UTF-8 passes unchanged, and a
      `<setenv>` value is converted by the same rule as an argument.
- [ ] `dotnet build -warnaserror` is clean for both Conformance projects, the fast tests are green,
      and `Surl.Conformance.UnitLibrary` keeps 100% line and branch coverage.
- [ ] A local Windows run of `UpstreamTestCasesAgainstSurlTests` still passes every case on
      `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
