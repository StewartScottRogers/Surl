---
id: BL-1334
title: Record libtest stdout and stderr when case 2407 times out on Linux
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, .github/workflows/ci.yml]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-1334 — Record libtest stdout and stderr when case 2407 times out on Linux

## Goal

When an upstream test case's client is stopped at its timeout, the stdout and stderr it wrote so far reach the CI results artifact, so the cause of a timeout such as case 2407 on Linux can be read.

## Context

BL-1333 found that `upstream-test-case-results-linux-x64` from CI run 44002670831 records only `test2407 fail timeout`. `UpstreamCurlRunner.RunAsync` already returns `StoppedAtTimeout` with the output so far, but the results file keeps just the verdict. Case 2407 runs `lib2405` (curl_multi_waitfds over HTTP/2 through nghttpx to Surl) from the pinned Debug libtests build. Once the output is in the artifact, read it, fix Surl if the cause is a Surl defect, and record the cause in `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md`.

## Acceptance criteria

- [x] A client stopped at its timeout leaves its stdout and stderr so far in a file next to the results file, covered by a unit test.
- [x] The CI workflow uploads that file in the `upstream-test-case-results-<rid>` artifact.

## Notes

Filed by BL-1333.

Third criterion (record the cause of the 2407 timeout) moved to BL-1335: the cause can only be read from an artifact a CI run produces after this change merges, so it cannot be done in this run.

Added `.github/workflows/ci.yml` to `touches`: the upload path lives there. New file `upstream-test-case-timeout-output.<platform>.txt` (class `UpstreamTestCaseTimeoutOutput`, in the UnitTests project) is written beside the failures file.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. A timed-out upstream test case's stdout and stderr now go to upstream-test-case-timeout-output.<platform>.txt and CI uploads it
