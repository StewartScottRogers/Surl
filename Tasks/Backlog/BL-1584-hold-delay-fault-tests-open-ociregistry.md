---
id: BL-1584
title: Hold delay-fault tests open: OciRegistry
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Surl.Extended.Protocol.OciRegistry.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-1584 — Hold delay-fault tests open: OciRegistry

## Goal

Every delay-fault unit test in Surl.Extended.Protocol.OciRegistry.UnitTests holds its delay with a scripted client that stays open, so the hold is never ended early by IConnection.WatchForPeerCloseAsync reporting the client gone.

## Context

Split from BL-1572 (OciRegistry and S3), whose run timed out with the work for this project nearly done. ADR-0141 amendment 2: a delay hold ends when the client half-closes or resets. A test whose scripted InMemoryConnection uses the default peerHalfClosesWhenExhausted: true reads as a client that left as soon as its script is consumed, so the hold can end before the test advances the fake clock, and the test flakes. Reference fix: BL-1569 (Amqp1), with commits b32c149e2 (DnsQuery) and 1a1f019db (Ssh).

The only delay test found in this project is `FindAnswerAsync_DelayRuleAtTheCommandStage_HoldsTheRequestThenAnswersAsWithoutIt` in `OciRequestFaultTests.cs`. Tests that deliberately prove a half-close or reset ENDS a hold keep their half-closing client. Only tests change: no production code, no other project.

### Reuse the stash

The timed-out run left its work in the stash named "darkfactory BL-1572 20261121-175153" (find N with `git stash list`, matching that message). It adds to `OciRegistryServerHarness.cs` a `ClientStaysOpen` property (passes `peerHalfClosesWhenExhausted: !ClientStaysOpen`), a linked serving CancellationTokenSource fed into `Context()`, `WaitForCompleteResponseAsync` and `StopServing`, and sets `ClientStaysOpen = true` plus wait/stop in the test. Take both tracked files with:

```
git diff "stash@{N}^" "stash@{N}" -- Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests/OciRegistryServerHarness.cs | git apply
git diff "stash@{N}^" "stash@{N}" -- Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests/OciRequestFaultTests.cs | git apply
```

The run had not yet built or tested this. Review it before keeping it: prefer `using System.Text.RegularExpressions;` and `System.Globalization` imports over fully qualified names, and check the polling wait is bounded and the serve task is awaited after `StopServing`.

## Acceptance criteria

- [ ] Every non-deliberate-half-close delay test in Surl.Extended.Protocol.OciRegistry.UnitTests uses a client that stays open through the hold (peerHalfClosesWhenExhausted: false or the harness's ClientStaysOpen), cancelling and awaiting the serve task at the end, as BL-1569 does.
- [ ] The changed tests pass 20 runs in a row locally with the filter FullyQualifiedName~DelayRule.
- [ ] dotnet build of Surl.Extended.Protocol.OciRegistry.UnitTests is clean with -warnaserror.
- [ ] dotnet test --filter "TestCategory!=Integration" is green for Surl.Extended.Protocol.OciRegistry.UnitTests.
- [ ] No file outside Surl.Extended.Protocol.OciRegistry.UnitTests changed.

## Notes

Run the checks on a build that includes this repo's current Surl.Protocol.Abstractions InMemoryConnection.

## Log

- 2026-11-21: Created (split from BL-1572 after its run timed out).
