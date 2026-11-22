---
id: BL-1416
title: Find why curl's connect to surl past --max-connections is refused on osx-arm64 during the full conformance run
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1416 — Find why curl's connect to surl past --max-connections is refused on osx-arm64 during the full conformance run

## Goal

Pinned upstream curl, connecting to surl while `--max-connections 1` is used up, always gets surl's `421` and exits
28 on osx-arm64, as it does on every other platform. The cause of the refused connect is found and fixed, not
tolerated.

## Context

- `UpstreamCurlFetchesFromSurlOverFtpTests.Download_MaxConnections1WhileAnotherClientHoldsIt_ExitsOperationTimedOut`
  expected 28 and got 7 (`curl: (7) Failed to connect to 127.0.0.1:<port> after 6-8 ms: Could not connect to server`)
  in two full runs of "Conformance against the published osx-arm64":
  - Release run 44640744739 (tag `v0.1.2`), port 49651
  - Release dry run 44745393942 (PR #117, 2026-11-20 13:59 UTC), port 49647, the first full run after BL-1413
    tightened the test back to 28 alone
- BL-1413's 50-repeat test (Release dry run 44672857143) passed 100 of 100 on osx-arm64 and osx-x64. So the refusal
  shows up in the full suite, where many tests run at once, not in a repeat of this test alone. osx-x64 and every
  other platform have not shown it.
- BL-1412 traced surl's side: accept, write `421 Too many connections` (`FtpProtocolServer.cs:184-190`, from
  `ServingEngine.cs:665-681`), lingering close (`StreamConnection.cs:272-374`). That path cannot make curl's
  *connect* fail. A connect refused within 6 ms on `127.0.0.1` means nothing was listening on that port, or the
  SYN was reset:
  - the published surl process exited or closed its listening socket (e.g. an accept failure not absorbed by
    `AcceptRace`, ADR-0022, ending `TcpConnectionListener.AcceptAsync` with `IOException`), or
  - the accept backlog was full under load (`TcpConnectionListener.cs:138`, `socket.Listen()` with the default
    backlog).
- The test captures curl's output but not surl's stderr or exit status at the moment of failure.

## Acceptance criteria

- [x] When this test fails, its message carries surl's stderr (with `-v`) and whether the surl process is still
      running, so the next failure names its cause. Done through the conformance test's surl helper, for every
      max-connections test in the suite.
- [ ] The cause is found from that evidence, with a Release dry run dispatched on the shift branch
      (`dispatch-workflow.ps1`; park as Blocked "Waiting for run:<id>"; repeat if the run passes without reproducing),
      and written in this task's Log with run ids and file:line.
- [ ] Surl is fixed so the listener stays up and keeps accepting past the limit, with a named fast test pinning the
      fix (e.g. an accept failure that must not end the listener), or, if the backlog is the cause, the backlog is
      set explicitly with the reason recorded in an ADR amendment.
- [ ] The test still asserts exit 28 alone on every platform. No tolerance, retry or `OSCondition` skip.
- [ ] A Release dry run passes "Conformance against the published osx-arm64" with the fix; its run id is in the Log.
- [ ] `dotnet build` clean, fast tests green, coverage gates held.

## Notes

- Same family as BL-1387 (IMAP max-connections), BL-1412 and BL-1413. Check whether the IMAP, SMTP and POP3
  max-connections tests share the cause.
- Until this lands, a v0.1.3 tag may fail on this test. Rerunning the failed job is the stopgap, as for v0.1.2.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Diagnostics landed (SurlOnLoopback.Diagnosis: surl process state + stderr; the four max-connections tests start surl with -v and show it in the failure message). Fast Conformance tests green (1053). The cause needs a Release dry run of the pushed commit; this lane cannot push, so the next run must dispatch it (dispatch-workflow.ps1 -Workflow release.yml -Ref factory branch) once this commit is on the shift branch, then block on run:<id>.
- 2026-11-20: Doing -> Backlog. Diagnostics committed; the Release dry run that names the cause must be dispatched from the pushed commit, which this lane cannot push.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Blocked. Waiting for run:44776460226 (Release dry run on factory/phase-1 with the max-connections diagnostics)
- 2026-11-20: Blocked -> Backlog. Run 44776460226 completed: failure; read it with gh run view 44776460226
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Blocked. Waiting for run:44788839369 (release.yml dry run on factory/phase-1; the previous run 44776460226 had its osx-arm64 publish cancelled, so conformance never ran)
- 2026-11-20: Blocked -> Backlog. Run 44788839369 completed: success; read it with gh run view 44788839369
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Run 44788839369 (release.yml on factory/phase-1, the diagnostics commit) passed, including "Conformance against the published osx-arm64": the refusal did not reproduce, so no cause is named yet. The earlier run 44776460226 never ran osx-arm64 conformance (its publish was cancelled; artifact not found). The per-task dispatch cap (2 per 12 h, dispatches at 16:44Z and 18:01Z) refuses a third until 2026-11-21 04:45 UTC. Next run: dispatch release.yml again on the shift branch after that time, block on run:<id>, and repeat until a failure shows surl's stderr and process state; then fix, pin with a fast test, and ADR if the backlog is the cause.
- 2026-11-20: Doing -> Backlog. Run 44788839369 passed without reproducing; dispatch cap (2 per 12h) blocks another Release dry run until 2026-11-21 04:45 UTC
- 2026-11-20: Reproduced with the diagnostics in Release run 44827681338 (dry run on PR #119, "Conformance against the published osx-arm64", 22:49 UTC): curl `(7) Failed to connect to 127.0.0.1:49656 after 0 ms`; surl process still running; surl -v shows the holder's exchange 1 (220, NOOP, 200) then `#- * Refused a connection from 127.0.0.1:49667: past --max-connections 1.`. So surl accepted curl's connection and took the refusal path (`ServingEngine.RefuseAsync`, ServingEngine.cs:664-680: FtpProtocolServer's 421 within the refusal deadline, then `DisposeAsync`; `Abort` only if the writer failed). The listener is fine and the backlog is not the cause: curl's connect completed at TCP level and was then reported failed, which points at the refusal's close reaching curl as a reset before curl's connect check (e.g. the 421 write failing or timing out, so `Abort`, or a reset from the close itself). The run's evidence is enough to work from; no new dispatch is needed to name the cause.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Backlog. Surl now logs when a refusal write fails and the connection is reset (ServingEngine.RefuseAsync); cause still unnamed. Needs that commit pushed, then a Release dry run (dispatch cap lifts 2026-11-21 04:45 UTC) to see whether surl reset the connection
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Backlog. Reset-logging commit e293e3b72 is unpushed from this lane and the dispatch cap (2 per 12h) lifts 2026-11-21 04:45 UTC; next run dispatches release.yml on the shift branch, blocks on run:id, and reads surl's stderr for the reset note
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Backlog. Reset-logging commit e293e3b72 is pushed (on origin/factory/phase-1) but the dispatch cap (2 per 12h) still refuses until 2026-11-21 04:45 UTC; next run dispatches release.yml on factory/phase-1, blocks on run:id and reads surl's stderr for the reset note
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Blocked. Waiting for run:44885355387 (release.yml dry run on factory/phase-1 with the reset logging; read surl stderr for the refusal reset note)
- 2026-11-21: Blocked -> Backlog. Run 44885355387 completed: success; read it with gh run view 44885355387
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Backlog. Run 44885355387 passed without reproducing; max-connections tests now run curl with -v so a failure names curl's connect errno (unpushed from this lane). Next run: dispatch release.yml on the shift branch once that commit is pushed (1 of 2 dispatches used in the last 12h), block on run:id, read curl -v and surl stderr
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Blocked. Waiting for run:44891838149 (release.yml dry run on factory/phase-1 with curl -v in the max-connections tests; read curl's connect errno and surl stderr)
- 2026-11-21: Blocked -> Backlog. Run 44891838149 completed: failure; read it with gh run view 44891838149
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Backlog. Run 44891838149 did not reproduce on osx-arm64 (its only failure was Publish linux-arm64; all five conformance jobs passed, osx-arm64 included); dispatch cap (2 per 12h) refuses another Release dry run until about 21:45 UTC 2026-11-21; next run dispatches release.yml on factory/phase-1, blocks on run:id and reads curl -v and surl stderr
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Backlog. Dispatch cap (2 per 12h; dispatches 08:32Z and 09:55Z) refuses another Release dry run until about 20:32 UTC 2026-11-21; next run dispatches release.yml on factory/phase-1, blocks on run:id and reads curl -v and surl stderr
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Backlog. Dispatch cap (2 per 12h; dispatches 08:32Z and 09:55Z) still refuses a Release dry run until about 20:32 UTC 2026-11-21; next run dispatches release.yml on factory/phase-1, blocks on run:id and reads curl -v and surl stderr
- 2026-11-21: Reproduced with curl -v in Release run 44920264373 (dispatched for BL-1565, head with the -v diagnostics), "Conformance against the published osx-arm64", this time IMAP: `UpstreamCurlReadsMailFromSurlOverImapTests.Fetch_MaxConnections1WhileAnotherClientHoldsIt_ExitsWeirdServerReply` (:509) expected 8, got 7. curl -v: `Trying 127.0.0.1:50049...` / `connect to 127.0.0.1 port 50049 from 127.0.0.1 port 50063 failed: Undefined error: 0` / `Failed to connect to 127.0.0.1:50049 after 0 ms`. surl was still running, and logged `Refused a connection from 127.0.0.1:50063: past --max-connections 1.`. So the TCP handshake completed and surl accepted and refused curl's very connection (same source port 50063), but curl's own connect check failed with errno 0. Reading: on macOS, surl's refusal (reply, then close) reaches curl before curl verifies its non-blocking connect, and curl's verify on macOS treats the hang-up as a failed connect (SO_ERROR 0, hence 'Undefined error: 0') instead of reading the refusal. So this is not FTP-specific, and it is timing between surl's close and curl's connect verify. Fix direction to weigh: hold the refused connection open after writing the refusal until the client reads or half-closes (or a short bound), so curl's connect check sees an open socket. Check upstream curl 8.21.0 lib/cf-socket.c (verify_connect / cf_tcp_connect on macOS) to confirm the POLLHUP path. No more dispatches are needed to name the cause.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Fix committed (ec03182d1): ConnectionLimits.RefusalSettleDelay (50 ms from ComposeConnectionLimits) holds a refusal until curl's connect check has finished (ServingEngine.SettleBeforeRefusalAsync, ServingEngine.cs); ADR-0006 amendment 2; fast tests for the delay, shutdown during it and the property. Tests still assert the original exit codes. Remaining: once pushed, dispatch release.yml on the shift branch, confirm 'Conformance against the published osx-arm64' passes, record the run id.
- 2026-11-21: Doing -> Backlog. Fix committed (ec03182d1, refusal settle delay); needs it pushed, then a Release dry run on the shift branch to prove osx-arm64 conformance passes
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Blocked. Waiting for run:44949044537 (release.yml dry run on factory/phase-1 with the refusal settle delay fix ec03182d1; osx-arm64 conformance must pass)
- 2026-11-21: Blocked -> Backlog. run:44949044537 failed: osx-arm64 conformance passed with the settle-delay fix ec03182d1, but osx-x64 failed RunAsync_EveryCaseWithAComposedServerHalf (an upstream test case regressed). And PR #128's CI (run 44948989146) fails on all three platforms in Surl.Core.UnitTests: ServingEngineTests.ServeAsync_RefusalSettleDelay_EndsWithShutdownAndStillClosesTheConnection times out in Harness.StopAsync (ServingEngineTests.Limits.cs:594), the fix's own test, and ServeAsync_ClientHalfClosesDuringAnAcceptDelay_EndsTheExchangeAtOnce_AndFreesItsSlot times out (10 s). The settle delay apparently does not end on shutdown. Fix both, run the FULL fast suite (dotnet test --filter TestCategory!=Integration) before finishing, then re-prove on a Release dry run
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Backlog. Settle delay now also ends when accepting stops (ServingEngine.SettleBeforeRefusalAsync), fixing the two Core test timeouts; full fast suite green except a Gopher timing flake; needs push, then a Release dry run on the shift branch to prove osx-arm64 and osx-x64 conformance
- 2026-11-21: Backlog -> Doing.
