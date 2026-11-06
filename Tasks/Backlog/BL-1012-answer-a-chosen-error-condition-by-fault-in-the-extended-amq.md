---
id: BL-1012
title: Answer a chosen error condition by fault in the extended AMQP 1.0 container
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823, BL-785]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests]
requirement: FR-062
created: 2026-11-06
completed:
---
# BL-1012 — Answer a chosen error condition by fault in the extended AMQP 1.0 container

## Goal

An `error` fault rule naming `amqp1` or `amqp1s` (or one of their listeners) makes the AMQP 1.0 container refuse a
matching `attach` with the chosen error condition and description on the `detach`, and reject a matching
`transfer` with it on the `disposition` without storing the message, instead of serving it, and publish one
`FaultAppliedEvent`.

## Context

- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md)
  decisions 4 and 9 and its extended reply table: an error condition symbol and a description; `command` is the
  performative, `path` the address (node). Consult `ExchangeContext.Faults` per operation after login; publish
  `FaultAppliedEvent` (BL-823's contracts and doubles). A rule whose reply is not an error, or a `delay` rule at
  the command stage, is noted and not applied. Judged by the AMQP 1.0 specification (ADR-0083); unit tests replay
  bytes written from it.
- Split from BL-834 (timed out after 120 min, 2026-11-06), whose lane had this container green. Reuse from the
  stash whose message is `darkfactory BL-834 20261106-033059` (commit `0980dd737c0c07b309c7c1f49872a4a4719d51d5`,
  called `S`; find its index with `git stash list`):
  - untracked, with `git checkout "S^3" -- <path>`:
    `Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/Amqp1ContainerConnection.Faults.cs`,
    `.../Surl.Extended.Protocol.Amqp1.UnitLibrary/FaultListenUrl.cs`,
    `.../Surl.Extended.Protocol.Amqp1.UnitTests/Amqp1ContainerConnectionTests.Faults.cs`,
    `.../Surl.Extended.Protocol.Amqp1.UnitTests/FaultListenUrlTests.cs`.
  - tracked, with `git diff "S^" "S" -- Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary | git apply`
    (`Amqp1ContainerConnection.Links.cs`, `Amqp1ContainerConnection.Transfers.cs`; neither has changed on the
    shared branch since the stash's base - if `git apply` refuses, re-make the three lines by hand).
- `Surl.Cli` still refuses these schemes' `reply` until BL-1015; the tests here build the fault plan directly.
- Stand-in: the shared message bus is unchanged; the new observable is `FaultAppliedEvent` on the instance's
  event stream, and tests inject the fault through the fault plan double.

## Acceptance criteria

- [ ] `Amqp1ContainerConnectionTests.Faults` exists and passes, including
      `ServeAsync_AttachToARulesNode_IsRefusedWithTheChosenErrorAndAnotherIsAttached` and
      `ServeAsync_TransferToARulesNode_IsRejectedWithTheChosenErrorAndNotStored`.
- [ ] Each applied rule publishes exactly one `FaultAppliedEvent`; `ServeAsync_ARuleWhoseReplyIsNotAnError_IsNotedAndTheAttachServed` passes.
- [ ] With no rule, every existing `Surl.Extended.Protocol.Amqp1.UnitTests` test passes unchanged.
- [ ] `dotnet build Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests -warnaserror` is clean, the fast
      tests are green, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-834 (timed out after 120 min); reuses its AMQP 1.0 work from stash `darkfactory BL-834 20261106-033059`.
