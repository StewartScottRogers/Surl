---
id: BL-1414
title: Find why upstream test case 2090 (HTTPS with SSLKEYLOGFILE) sometimes records nothing on linux-x64, and make it pass every run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1414 — Find why upstream test case 2090 (HTTPS with SSLKEYLOGFILE) sometimes records nothing on linux-x64, and make it pass every run

## Goal

Upstream test case 2090 passes on every linux-x64 run, so `UpstreamTestCasesAgainstSurlTests.RunAsync_EveryCaseWithAComposedServerHalf_WritesOneResultLinePerCase`
stops failing CI now and then on a case listed as passing.

## Context

- CI run 44669095406 (ci on `factory/phase-1`, 2026-11-20 03:41 UTC), job "Upstream test cases (ubuntu-latest)":
  `test2090 fail protocol: the server half recorded nothing`, so the regression guard failed
  (`Conformance/Surl.Conformance.UnitTests/UpstreamTestCasesAgainstSurlTests.cs:169`,
  `Assert.IsFalse(comparison.HasRegressions)`). Totals: 1,326 run, 1,325 pass, 1 fail. 2090 is listed in
  `Documentation/Conformance/upstream-test-cases-passing.linux-x64.txt`. The five CI runs on `master` after it
  (2026-11-19 18:37 to 2026-11-20 12:10 UTC) all passed, so the failure is intermittent.
- Case 2090 (`Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/data/test2090`): "HTTPS request with
  SSLKEYLOGFILE set", `--cacert %CERTDIR/certs/test-ca.crt --tls-max 1.2 https://localhost:%HTTPSPORT/2090`.
- The message comes from `Conformance/Surl.Conformance.UnitLibrary/UpstreamTestCaseVerifyChecker.cs:220`: surl's
  server half logged no request. Either curl never reached surl (for example `localhost` resolving to `::1` while
  surl listened on `127.0.0.1` only, or a TLS 1.2 handshake failing), or the request was recorded after the
  checker read the log (a race like BL-1375's).

## Acceptance criteria

- [x] The task's Log names the cause with evidence: curl's stderr and exit code for the failing run (from the
      results file or the job log, rerunning the job with more logging if needed), and the file:line in the
      runner or checker it points to.
- [x] The cause is fixed in `Surl.Conformance.UnitLibrary` (or in surl itself if surl dropped the connection), with
      a named fast test pinning the fix (for a race: the checker waits, within a bound, for the server half's
      record before judging). Fixed in the test project's server halves instead (see Notes).
- [x] Case 2090 stays in `upstream-test-cases-passing.linux-x64.txt`. It is not dropped or skipped to make the
      guard pass.
- [x] A dispatched `ci` run on the shift branch passes "Upstream test cases (ubuntu-latest)": moved to BL-1415, because a lane may not push, so the fix cannot be on the branch yet.
- [x] `dotnet build` clean, fast tests green, coverage gates held (the fix is in the test project; no UnitLibrary changed).

## Notes

- Cause: CI run 44669095406's results artifact `upstream-test-case-failures.linux-x64.txt` holds, for test2090,
  `curl exit code: 35`, `curl: (35) TLS connect error: error:0A00010B:SSL routines::wrong version number`, and empty
  server notes. curl spoke TLS to something that was not the TLS listener. `localhost` resolves to `::1` first;
  the `wss` (HTTPS) listener binds only `127.0.0.1` (`UpstreamTestCaseServerHalves.cs` `Listeners`), and every
  listener draws its port from the OS on its own, so now and then an IPv4-only TLS listener draws the same
  number as a plaintext `::1` listener (http, ftp, gopher, imap, pop3, smtp). curl then reaches that plaintext
  server. The checker (`UpstreamTestCaseVerifyChecker.cs:220`) only reports the consequence. Inference: the
  collision was not observed directly; it fits the evidence and nothing else in the log does.
- Fix: `UpstreamTestCaseServerHalves.StartAsync` binds again (up to 20 draws) while
  `UpstreamTestCasePortCollisions.AnyAcrossHosts` finds two listeners of different schemes on different hosts
  sharing a port. It protects every case, not only 2090. Pinned by `UpstreamTestCasePortCollisionsTests`.
  It sits in `Surl.Conformance.UnitTests`, not the library, as the listeners are bound there.

- If the evidence shows a race in how the checker reads the server half's log, check whether other cases could
  hit the same race and fix it once for all of them.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. 2090's intermittent exit 35 traced to IPv4 TLS / ::1 plaintext port collision; server halves redraw ports on collision; CI confirmation is BL-1415
