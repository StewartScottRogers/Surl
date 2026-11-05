---
id: BL-392
title: Run every runnable http and https upstream test case against surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-389, BL-390, BL-391]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, .github/workflows/ci.yml]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-392 — Run every runnable http and https upstream test case against surl

## Goal

An integration test in `Surl.Conformance.UnitTests` runs every runnable `http` and `https` upstream
test case. For each case it starts surl's server half, runs the pinned reference build with the
case's command line as runtests would build it, checks the verify section, and writes one result line
per case: test number, pass, fail or skip, and the reason.

## Context

- Decision: BL-386's ADR, decisions 5 and 6 (results format, and where the suite runs).
- Pieces:
  - the parser (BL-388);
  - the selection (BL-389);
  - the checker (BL-390);
  - the HTTP server half (BL-391);
  - `PinnedUpstreamCurl` and `UpstreamCurlRunner` (pinned builds only, ADR-0003).
- How runtests builds the command (`tests/runner.pm` at tag `curl-8_21_0`): `-q` first unless
  `option="no-q"` (line 975), `--output` unless the case verifies stdout or says `no-output` (lines
  829 to 832), `--include` unless `no-include` (line 868), and `--trace-ascii` into the log directory
  (line 877). `<file>` and `<stdin>` are prepared before the run. `%LOGDIR` is a fresh temporary
  directory per case.
- The test is `[TestCategory("Integration")]`, and is inconclusive where no pinned build is installed,
  as the existing conformance tests are.
- If the ADR runs the suite in CI, add the step to `.github/workflows/ci.yml` within the ADR's time
  budget.

## Acceptance criteria

- [x] Running `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~UpstreamTestCases"`
      (or the class the ADR names) on Windows runs every runnable `http` and `https` case. It writes
      the results file the ADR names, and Notes record the pass, fail and skip counts.
- [x] A case that hangs is ended by a per-case timeout and recorded as a failure with reason
      `timeout`. The run never hangs.
- [x] Each failing case is either fixed in surl within this task, if it is a one-line defect, or
      grouped by cause into new tasks filed with `task-planner`, with their IDs in Notes. This task
      does not widen into fixing them all.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Results, Windows, reference build (`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
  0E7737...8778), 2026-11-04, three consecutive runs identical: of 2,013 cases, **686 ran (every
  runnable case whose servers are only `http`/`https`): 651 pass, 35 fail (2 of them `timeout`);
  1,327 skip**. The run takes about 75 s at 16 cases at once. Results file:
  `upstream-test-case-results.win-x64.txt` in the test run's results directory (`TestRunResultsDirectory`),
  attached to the test result. Skips: decision 3's reasons (libtest 349, no-server-half 87,
  servercmd-unsupported 194 across 26 commands, feature-missing 128, server-not-built 67, ...),
  `server-half-not-composed:<name>` 293 for the cases whose server half is another task's (ftp 122,
  smtp 33, imap 31, sftp 24, ...). No line reads `skip unparsable` in this run.
- Failures, grouped by cause and filed (none was a one-line defect):
  - BL-680: part number from the digits after the target's last `/`, query included (16 cases:
    11, 28, 40, 43, 187, 276, 309, 1024, 1025, 1031, 1051, 1052, 1053, 1067, 1138, 1168). The fix is
    a rule change in `HttpUpstreamTestCasePartNumber` with its tests and docs, outside this task's
    touches.
  - BL-681: PROXY protocol header before the request (1455, 3201, 3220).
  - BL-682: test certificates from the imported `.prm` files (417, 2033, 2079, 2087).
  - BL-683: the remaining twelve, each to be explained (30 and 37 time out on an empty `<data>`;
    60, 173, 329, 433, 470, 1064, 1065, 1147, 1244, 2081).
- Where the code went: `Surl.Conformance.UnitLibrary` gains `UpstreamTestCaseCommandLine` (+
  `UpstreamTestCaseClientFile`), `UpstreamTestCaseShellWords`, `UpstreamTestCaseResult` (+
  `UpstreamTestCaseOutcome`) and a working-directory overload on `UpstreamCurlRunner`;
  `Surl.Conformance.UnitTests` gains `HttpUpstreamTestCaseServerHalves` (the engine composition)
  and `UpstreamTestCasesOverHttpTests`. `.github/workflows/ci.yml` gains the `Upstream test cases`
  job (ubuntu, macOS; 20-minute step in a 30-minute job; results uploaded as an artifact) and the
  existing conformance step excludes `TestCategory=UpstreamTestCases`, as decision 6 says.
- Choices with a sensible default (implementation detail inside ADR-0080, no new ADR):
  - Each case runs in its own folder standing for runtests' `tests` folder: curl's working
    directory, `%PWD`, `HOME`/`CURL_HOME`/`XDG_CONFIG_HOME`/`USERPROFILE`, with `%LOGDIR` the relative
    `log`, as runtests runs one case at a time. The first run used absolute temporary paths; the
    machine's `%TEMP` holds a blank (`Stewart Rogers`), which split the cases' unquoted paths
    (270 failures, down to 49), and `-O` wrote into the test process' folder. The run folder is
    therefore under the test assembly's folder, deleted afterwards.
  - curl's stdout and stderr are written to `log/stdout<N>` and `log/stderr<N>` after the run,
    since cases verify them as files (13 more passes).
  - The TLS listener is registered for `wss`, which offers ALPN `http/1.1` only: Surl's `https`
    listener offers `h2` first (ADR-0081), which would take the exchange from the HTTP/1.x server
    half, while upstream's https test server is HTTP/1.1 behind TLS.
  - Per-case limit: 60 s for every case (decision 6's default); runtests' `<command timeout=>` is a
    server-log lock wait, not curl's limit, so it is not used.
  - `<setenv>` with no `=` (runtests deletes the variable) sets it empty, since the runner only sets.
  - The results line for a `<file>` failure names the section only (`file`), not its path; the
    full difference goes to the test log.
  - Which cases pass is reported, not asserted; the test asserts one line per case and at least one
    pass. "Only rises" is BL-393's guard.
- Coverage (`Measure-CodeQuality -SkipTestRun` over this project's Cobertura):
  `Surl.Conformance.UnitLibrary` 100% line, 100% branch; its 8 members over 10 in Cobertura's
  complexity are the ones BL-391 listed, none new.
- `runner.pm` at tag `curl-8_21_0` was downloaded with `Invoke-WebRequest` for reading (a document,
  not a run against Surl).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. UpstreamTestCasesOverHttpTests runs every runnable http and https upstream test case against Surl with the pinned reference build and writes upstream-test-case-results.<platform>.txt (Windows: 686 ran, 651 pass, 35 fail filed as BL-680 to BL-683); CI runs it on Linux and macOS
