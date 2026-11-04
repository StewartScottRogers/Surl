---
id: BL-356
title: Find why KexInit_CompressedSsh_ListsZlibBeforeNone fails intermittently on macOS and fix it
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-356 — Find why KexInit_CompressedSsh_ListsZlibBeforeNone fails intermittently on macOS and fix it

## Goal

`UpstreamCurlOffersSshAlgorithmsTests.KexInit_CompressedSsh_ListsZlibBeforeNone` passes or fails on
what the recorder read from pinned upstream curl, never on timing, and a failure says why curl exited.

## Context

- Evidence: CI run 36895050896 (pull request #17, commit `b8c3813d`), attempt 1, job "Build and test
  (macos-latest)" (job ID 110479824334), step "Conformance tests". This test was the only failure of
  623, after 111 ms: `Assertion failed. curl exited before it sent its KEXINIT.` at
  `RecordKexInitAsync` (`Surl.Conformance.UnitTests/UpstreamCurlOffersSshAlgorithmsTests.cs`, line 120).
  TestContext held only `curl -sS -k --compressed-ssh sftp://127.0.0.1:49633/x: upstream curl
  04E0E69B... exited 2`, with no stderr text. Attempt 2 of the same run passed on every leg. Fetch the
  log with `gh api repos/StewartScottRogers/Surl/actions/jobs/110479824334/logs`.
- Exit 2 is `CURLE_FAILED_INIT`. In upstream curl 8.21.0 (tag `curl-8_21_0`, `lib/vssh/libssh2.c`
  lines 1485-1491), the libssh2 backend returns it when `libssh2_session_handshake` fails, after
  `failf(data, "Failure establishing ssh session: %d, %s", ...)`. The handshake is what sends the
  client's `KEXINIT`, so exit 2 alone does not prove curl never sent one.
  `ClientKexInitRecorder.RecordAsync` (`Surl.Conformance.UnitTests/ClientKexInitRecorder.cs`) hangs
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

- [ ] Notes state the cause and its evidence: curl's stderr from a reproduction, or the reasoning from
      the code when it cannot be reproduced.
- [ ] `RecordKexInitAsync` reports "curl exited before it sent its KEXINIT" only when, once curl has
      exited, the recorder accepted no connection or read no complete `KEXINIT`. It waits for a
      recorder that has read the `KEXINIT`, whatever curl's exit. Which task finished first no
      longer decides the outcome.
- [ ] Every failure message from `RecordKexInitAsync` includes curl's exit code and stderr text.
- [ ] A cause inside `ClientKexInitRecorder` is fixed there. No retry is added that would hide a real
      failure.
- [ ] `dotnet build Surl.Conformance.UnitTests -warnaserror` is clean and the fast tests are green.
      `dotnet test Surl.Conformance.UnitTests --filter "FullyQualifiedName~UpstreamCurlOffersSshAlgorithmsTests"`
      passes 20 times in a row on Windows, and Notes record the loop and its result.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
