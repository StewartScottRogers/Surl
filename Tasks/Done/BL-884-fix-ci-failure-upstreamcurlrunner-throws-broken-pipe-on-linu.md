---
id: BL-884
title: Fix CI failure: UpstreamCurlRunner throws Broken pipe on Linux when curl exits before reading its standard input
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-884 — Fix CI failure: UpstreamCurlRunner throws Broken pipe on Linux when curl exits before reading its standard input

## Goal

`UpstreamCurlRunner.RunAsync` reports the exit code, stdout and stderr of a curl that exits
without reading its standard input, instead of throwing, so the `Upstream test cases (ubuntu-latest)`
CI job passes and the frozen end-of-shift pull request can merge to `master`.

## Context

- CI run 42127054968 (pull request #19, frozen commit `fbfd41f`) failed only in
  `Upstream test cases (ubuntu-latest)`:
  `UpstreamTestCasesAgainstSurlTests.RunAsync_EveryCaseWithAComposedServerHalf_WritesOneResultLinePerCase`
  threw `System.IO.IOException: Broken pipe` from
  `UpstreamCurlRunner.RunAsync` -> `process.StandardInput.BaseStream.WriteAsync(standardInput, …)`
  (`Conformance/Surl.Conformance.UnitLibrary/UpstreamCurlRunner.cs`, about line 132).
- Cause: some upstream test cases make curl exit (or never read stdin) before the runner has
  written all of `standardInput`. On Linux and macOS writing to a pipe whose reader has gone
  raises `EPIPE` ("Broken pipe"); on Windows the same race raises `IOException` ("The pipe is
  being closed" / "The pipe has been ended") less often. macOS passed this run by timing, not by
  design.
- The `testNNNN: fail protocol: …` lines in that job's log are the pass-rate report and are
  expected; they are not the failure.
- This blocks every end-of-shift merge (ADR-0047 amendment 2): `Update-PendingMerges` merges
  only when CI passed. Once this lands, the next shift's frozen pull request should pass.

## Acceptance criteria

- [x] `UpstreamCurlRunner.RunAsync` writes standard input concurrently with reading stdout and
      stderr (or after starting both reads), and treats an `IOException` from writing or closing
      standard input as "curl did not read it": the run still returns curl's exit code, stdout and
      stderr. Any other exception still propagates.
- [x] A test in `Conformance/Surl.Conformance.UnitTests/UpstreamCurlRunnerTests.cs` reproduces it
      on every platform without the network: it runs a process that exits at once without reading
      stdin (e.g. the current `dotnet` host with `--version`, via a test `UpstreamCurlLocation`) and
      passes at least 4 MiB of standard input, and asserts the exit code 0 and that no exception is
      thrown. If a process start needs `TestCategory("Integration")` under the testing rules, mark it
      so, and keep a fast test of the catch through a seam instead.
- [x] `Surl.Conformance.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green; the upstream test-case
      tests in `UpstreamTestCasesAgainstSurlTests` pass locally on Windows.

## Notes

Filed by the interactive session on 2026-11-05 after reading CI run 42127054968.

- Delivered directly rather than through the full /feature stages: a one-method fix in one
  library, no new surface, no curl bytes involved.
- `RunAsync` now starts the stdout copy and the stderr read before writing stdin, then calls the
  new internal `UpstreamCurlRunner.WriteStandardInputAsync(StreamWriter, bytes, token)`: it writes
  the bytes to the writer's base stream and closes it; an `IOException` from either is "curl did
  not read it", and the close is retried with its own `IOException` ignored. Any other exception
  propagates. It takes the `StreamWriter` (not its base stream) so `Process.Dispose` never flushes
  a writer over an already-disposed stream.
- Fast tests drive it through the hand-written `ClosedPipeStream` (throws on write, on close, or
  both); the `Integration` test
  `RunAsync_ProcessExitsWithoutReadingFourMebibytesOfStandardInput_ReturnsItsExitCode` runs the
  `dotnet` host (`DOTNET_HOST_PATH`) with `--version` and 4 MiB of stdin and passes on Windows.
- `UpstreamTestCasesAgainstSurlTests` passed locally on Windows (1 m 31 s). Coverage of the new
  methods: 100% line and branch.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. UpstreamCurlRunner returns curl's result when curl exits without reading its stdin, instead of throwing Broken pipe
