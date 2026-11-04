---
id: BL-356
title: Find why KexInit_CompressedSsh_ListsZlibBeforeNone fails intermittently on macOS and fix it
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-356 — Find why KexInit_CompressedSsh_ListsZlibBeforeNone fails intermittently on macOS and fix it

## Goal

`UpstreamCurlOffersSshAlgorithmsTests.KexInit_CompressedSsh_ListsZlibBeforeNone` passes or fails on
what the recorder read from pinned upstream curl, never on timing, and a failure says why curl exited.

## Context

- Evidence: CI run 36895050896 (pull request #17, commit `b8c3813d`), attempt 1, job "Build and test
  (macos-latest)" (job ID 110479824334), step "Conformance tests". This test was the only failure of
  623, after 111 ms: `Assertion failed. curl exited before it sent its KEXINIT.` at
  `RecordKexInitAsync` (`Conformance/Surl.Conformance.UnitTests/UpstreamCurlOffersSshAlgorithmsTests.cs`, line 120).
  TestContext held only `curl -sS -k --compressed-ssh sftp://127.0.0.1:49633/x: upstream curl
  04E0E69B... exited 2`, with no stderr text. Attempt 2 of the same run passed on every leg. Fetch the
  log with `gh api repos/StewartScottRogers/Surl/actions/jobs/110479824334/logs`.
- Exit 2 is `CURLE_FAILED_INIT`. In upstream curl 8.21.0 (tag `curl-8_21_0`, `lib/vssh/libssh2.c`
  lines 1485-1491), the libssh2 backend returns it when `libssh2_session_handshake` fails, after
  `failf(data, "Failure establishing ssh session: %d, %s", ...)`. The handshake is what sends the
  client's `KEXINIT`, so exit 2 alone does not prove curl never sent one.
  `ClientKexInitRecorder.RecordAsync` (`Conformance/Surl.Conformance.UnitTests/ClientKexInitRecorder.cs`) hangs
  up as soon as it has read the `KEXINIT`, and that hang-up on its own makes the handshake fail and
  curl exit 2.
- `RecordKexInitAsync` awaits `Task.WhenAny(recording, curl)` and fails when `recording` is not yet
  complete. If curl's exit is observed before the recording task completes, the test reports exactly
  this message. This task finds out whether that happened, or whether curl really failed earlier (the
  banner write, macOS loopback). The helper serves all four tests in the class, so the fix covers all
  of them.
- The macOS pinned build is the `osx-arm64` reference build in `UpstreamCurlBuilds.json`
  (stunnel/static-curl 8.21.0, SHA-256 `04E0E69B...`). Lanes test on Windows only. The test has no
  `[OSCondition]`, so it can reproduce on any platform, if at all.

## Acceptance criteria

- [x] Notes state the cause and its evidence: curl's stderr from a reproduction, or the reasoning from
      the code when it cannot be reproduced.
- [x] `RecordKexInitAsync` reports "curl exited before it sent its KEXINIT" only when, once curl has
      exited, the recorder accepted no connection or read no complete `KEXINIT`. It waits for a
      recorder that has read the `KEXINIT`, whatever curl's exit. Which task finished first no
      longer decides the outcome.
- [x] Every failure message from `RecordKexInitAsync` includes curl's exit code and stderr text.
- [x] A cause inside `ClientKexInitRecorder` is fixed there. No retry is added that would hide a real
      failure.
- [x] `dotnet build Conformance/Surl.Conformance.UnitTests -warnaserror` is clean and the fast tests are green.
      `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~UpstreamCurlOffersSshAlgorithmsTests"`
      passes 20 times in a row on Windows, and Notes record the loop and its result.

## Notes

- Cause (from the code; not reproducible here: the Windows reference build is missing since
  2026-10-05, so this test is inconclusive on Windows, and the macOS failure was a one-off on CI).
  `ClientKexInitRecorder.RecordAsync` disposes its `TcpClient` (the `using var client`) when it
  returns, which is *before* the task it returns completes. That hang-up makes libssh2's handshake
  fail and curl exit 2 at once. The old `RecordKexInitAsync` then raced `Task.WhenAny(recording,
  curl)` and judged by `recording.IsCompleted`: a thread-pool thread descheduled between the
  dispose and setting the result (a loaded 3-core macOS runner) lets curl's exit be observed first,
  and the test fails with "curl exited before it sent its KEXINIT" although the KEXINIT was read.
  The CI log matches: exit 2 (what every passing run also gets), no stderr captured (the old
  message never included it), 111 ms. A real early failure (no connection) cannot be ruled out from
  that log, which is why every failure now carries curl's exit code and stderr.
- Fix, in `RecordKexInitAsync`: await curl first, then stop the listener (so a recorder that never
  accepted stops waiting), then await the recording. Once curl has exited, every byte it sent is in
  the recorder's receive buffer, so a recorder that accepted and got a complete KEXINIT always
  succeeds; one that did not throws (socket/IO/disposed), which becomes "curl exited before it sent
  its KEXINIT: the recorder stopped with <exception> (<curl exit>; stderr: <text>)". The recorder's
  own assertion failures get the same curl suffix. No retry. Nothing in `ClientKexInitRecorder`
  itself is wrong (its hang-up is what makes curl exit), so it is unchanged.
- Loop: `dotnet test Surl.Conformance.UnitTests --no-build --filter
  "FullyQualifiedName~UpstreamCurlOffersSshAlgorithmsTests"` 20 times in a row on Windows: 20
  passed, 0 failed (each: 1 passed - the OpenSSL supplementary build, which uses the same helper -
  and 3 skipped/inconclusive for the missing reference build and the non-Windows test).
- `dotnet build` clean (0 warnings), fast tests green.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. KEXINIT tests judge on what the recorder read, never on which task finished first; failures carry curl's exit and stderr
