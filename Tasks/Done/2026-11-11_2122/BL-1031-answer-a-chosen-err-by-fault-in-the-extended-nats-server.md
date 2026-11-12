---
id: BL-1031
title: Answer a chosen -ERR by fault in the extended NATS server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823, BL-785, BL-1035]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests]
requirement: FR-062
created: 2026-11-06
completed: 2026-11-11
---
# BL-1031 — Answer a chosen -ERR by fault in the extended NATS server

## Goal

An `error` fault rule naming `nats` or `natss` (or one of their listeners) makes the NATS server answer a matching
`PUB`, `HPUB` or `SUB` with `-ERR '<text>'` - delivering or subscribing nothing - instead of serving it, and
publish one `FaultAppliedEvent`.

## Context

- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md)
  decisions 4 and 9 and its extended reply table: `command` is the protocol verb, `path` the subject. Consult
  `ExchangeContext.Faults` per verb after `CONNECT`; a token login accepted unchecked carries no account to the
  plan. Publish `FaultAppliedEvent` (BL-823's contracts and doubles). A rule whose reply is not `-ERR` text, or a
  `delay` rule at the command stage, is noted and not applied. Judged by the NATS client protocol (ADR-0083);
  unit tests replay bytes written from it.
- Split from BL-834 (timed out after 120 min, 2026-11-06), whose lane had this server green. Reuse from the
  stash whose message is `darkfactory BL-834 20261106-033059` (commit `0980dd737c0c07b309c7c1f49872a4a4719d51d5`,
  called `S`; find its index with `git stash list`):
  - untracked, with `git checkout "S^3" -- <path>`:
    `Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary/NatsServerConnection.Faults.cs`,
    `.../Surl.Extended.Protocol.Nats.UnitLibrary/FaultListenUrl.cs`,
    `.../Surl.Extended.Protocol.Nats.UnitTests/NatsFaultTests.cs`,
    `.../Surl.Extended.Protocol.Nats.UnitTests/FaultListenUrlTests.cs`.
  - tracked, with `git diff "S^" "S" -- Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary | git apply`
    (`NatsServerConnection.cs`; unchanged on the shared branch since the stash's base - if `git apply` refuses,
    re-make it by hand).
- `Surl.Cli` still refuses these schemes' `reply` until BL-1015; the tests here build the fault plan directly.
- Stand-in: the shared message bus is unchanged; the new observable is `FaultAppliedEvent` on the instance's
  event stream, and tests inject the fault through the fault plan double.

## Acceptance criteria

- [x] `NatsFaultTests` exists and passes, including `ServeAsync_PubToARulesSubject_AnswersTheChosenErrAndAnotherSubjectIsServed`,
      `ServeAsync_HpubUnderARule_AnswersTheChosenErr` and `ServeAsync_SubUnderARule_AnswersTheChosenErrAndSubscribesNothing`.
- [x] Each applied rule publishes exactly one `FaultAppliedEvent`; `ServeAsync_ARuleWhoseReplyIsNotErrText_IsNotedAndNotApplied` passes.
- [x] With no rule, every existing `Surl.Extended.Protocol.Nats.UnitTests` test passes unchanged.
- [x] `dotnet build Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests -warnaserror` is clean, the fast
      tests are green, and the library keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 4): restored the four stashed files from `S^3` (via `git show`, since `git checkout` of a
  stash commit was refused in the lane) and applied the stash's `NatsServerConnection.cs` diff cleanly with
  `git apply`. Nothing could be built: `dotnet build` fails with "A compatible .NET SDK was not found" -
  `global.json` pins 10.0.401 and the machine has only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100. Parked
  behind BL-1035 (Stewart reinstalls the pinned SDK), as BL-1012 and BL-1007 were. The restored work is left
  uncommitted for the shift to stash; next run, redo the restore above if it is gone, then build and test.

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-834 (timed out after 120 min); reuses its NATS work from stash `darkfactory BL-834 20261106-033059`.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing builds
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. A NATS error fault rule answers a matching PUB, HPUB or SUB with -ERR '<text>' and publishes one FaultAppliedEvent
