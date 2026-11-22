---
id: BL-1573
title: Hold delay-fault tests open: Ftp and Smb
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Surl.Protocol.Ftp.UnitTests, Surl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-1573 — Hold delay-fault tests open: Ftp and Smb

## Goal

Every delay-fault unit test in Surl.Protocol.Ftp.UnitTests and Surl.Protocol.Smb.UnitTests holds its delay with a scripted client that stays open, so the hold is never ended early by IConnection.WatchForPeerCloseAsync reporting the client gone.

## Context

ADR-0141 amendment 2: a delay hold ends when the client half-closes or resets. A test whose scripted InMemoryConnection uses the default peerHalfClosesWhenExhausted: true reads as a client that left as soon as its script is consumed, so the hold can end before the test advances the fake clock, and the test flakes. Reference fix: BL-1569 (Amqp1), with commits b32c149e2 (DnsQuery) and 1a1f019db (Ssh). The pattern is: build the scripted client as new InMemoryConnection(frames, peerHalfClosesWhenExhausted: false) (or pass the equivalent flag of the project's own test exchange helper); give the exchange a linked CancellationTokenSource; wait for the visible outcome after the clock advance; then cancel and await serving. Scope: the tests using DelayRule, FaultKind.Delay or UnderADelay in Surl.Protocol.Ftp.UnitTests,Surl.Protocol.Smb.UnitTests (start with each project's *Fault*Tests file). Tests that deliberately prove a half-close or reset ENDS a hold keep their half-closing client. A project with no half-closing delay test after a look needs no change: say so in the Log. Only tests change: no production code, no other project. Run the checks on a build that includes this repo's current Surl.Protocol.Abstractions InMemoryConnection.

## Acceptance criteria

- [x] Every non-deliberate-half-close delay test in Surl.Protocol.Ftp.UnitTests,Surl.Protocol.Smb.UnitTests uses a client that stays open through the hold (peerHalfClosesWhenExhausted: false or the project helper's equivalent), cancelling and awaiting the serve task at the end, as BL-1569 does.
- [x] The changed tests pass 20 runs in a row locally with the filter FullyQualifiedName~DelayRule (add a name filter such as ~Delay where a test has no DelayRule in its name).
- [x] dotnet build of each project is clean with -warnaserror.
- [x] dotnet test --filter "TestCategory!=Integration" is green for each project.
- [x] No file outside the named .UnitTests projects changed.

## Notes

Changed: FtpFaultTests.Retr_UnderADelayRule_... and SmbCommandFaultsTests.Create_UnderADelayRule_... now use a client built with peerHalfClosesWhenExhausted: false, a linked CancellationTokenSource, a wait for the visible outcome, then cancel and await serving (Smb StartServing gained an overload taking the exchange token). FtpUpstreamTestCaseServerTests.Delay_* uses the upstream-test-case DELAY keyword, not a DelayRule/command-stage fault hold, so it is unchanged. WriteStallingConnection files are not delay-rule tests. 20 filtered runs per project passed; build -warnaserror clean; fast suites green (Ftp 557, Smb 226).

Filed by task-planner on 2026-11-21 for the delay-hold flake sweep that follows BL-1569.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing.
- 2026-11-21: Doing -> Done. Ftp and Smb delay-fault tests now hold with a client that stays open
