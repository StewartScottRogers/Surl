---
id: BL-977
title: Bind and serve the loopback control endpoint beside the protocol listeners in Surl.Composition
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1022, BL-972]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-977 — Bind and serve the loopback control endpoint beside the protocol listeners in Surl.Composition

## Goal

With `--control` given, `surl` (and a `Surl.Api` server given the option through `AdditionalArguments`) binds the
control endpoint on its loopback listen URL through the same `IListenerFactory` before the protocol listeners,
writes the start-up warning and the `Control endpoint listening on <bound URL>` status line, and answers it with
`ControlEndpoint` over `ComposedControlTarget` - its own exchanges unpublished and unlogged, at most 8
connections of its own, outside `--max-connections`.

## Context

- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md)
  decision 7, "Start": the warning
  `surl: warning: --control: a test control endpoint on <bound URL> can change this server's files, mail, messages, accounts and faults`
  (from the info level up, after the fault warning), the status line after the `Listening on` lines, a bind
  failure ending the start with its exit code (45) before any other line.
- Built on: BL-1022 (`ComposedControlTarget`), BL-972 (`ComposedAuthentication.ControlToken`), BL-814
  (`ControlEndpoint`, `FeedEventsAsync`).
- Split from BL-816 (timed out after 120 min, 2026-11-06), whose lane had these tests passing and was measuring
  coverage. Reuse from the stash whose message is `darkfactory BL-816 20261106-033059` (commit
  `0b6c48207fcfb912c5b903ddc55ed2a06d93e6da`, called `S`; find its index with `git stash list`):
  - untracked, with `git checkout "S^3" -- <path>`: `Application/Surl.Composition.UnitLibrary/ControlProtocolServer.cs`;
    and `Application/Surl.Composition.UnitTests/SurlServerCompositionTests.Control.cs` merged with what BL-972
    already added to it.
  - `MaxConnections`, `WriteWarning` and `FormatStatusLine` from the stash's `ControlComposition.cs` (`git show "S^3:<path>"`),
    added to the file BL-972 created.
  - tracked, by `git diff "S^" "S" -- <file> | git apply` (unchanged on the shared branch since the stash's
    base; re-make by hand if BL-1022 or BL-972 moved a line): `ComposedServing.cs` (+139: `ControlListenUrl`,
    `ServeWithControlAsync`, `ComposeControlEngine`, `ServeProtocolsAsync`, `RunEngineAsync`) and
    `Application/Surl.Composition.UnitTests/FakeListenerFactory.cs` (`ConnectionsFor(listenUrl)`, and a
    cancelled start refused as a real listener does).
- Update `Application/Surl.Composition.UnitLibrary/CLAUDE.md`'s ordered-start table with the token read and the
  control listener's bind.
- Stand-in: the control endpoint, live, over the instance's own event stream, fault plan and stores. BL-819 proves
  it with pinned upstream curl.

## Acceptance criteria

- [ ] Over `FakeListenerFactory` with `--control http://127.0.0.1:0` and a token file:
      `ServeAsync_Control_ListsTheHttpExchangeServedOnTheSameInstanceAndNotItsOwn`,
      `ServeAsync_ControlPostFaultsDrop_DropsTheNextHttpConnection`, `ServeAsync_ControlPostReset_EmptiesASeededFile`
      and `ServeAsync_AControlRequestWithoutTheToken_IsRefused401` pass.
- [ ] `ServeAsync_Control_WritesTheWarningAfterTheFaultWarningAndTheStatusLineAfterTheListeningLines` pins the
      warning and status line byte for byte; `ServeAsync_ControlWithSilent_WritesNeitherTheWarningNorTheStatusLine` passes.
- [ ] `ServeAsync_WithoutControl_HasNoControlListener`, `ServeAsync_AControlListenerThatCannotBind_EndsWith45BeforeAnyOtherLine`
      and `ServeAsync_ControlStoppedBeforeItBinds_EndsOkWithNoLine` pass.
- [ ] `Surl.Composition.UnitLibrary/CLAUDE.md`'s ordered-start table names the token read and the control bind.
- [ ] `dotnet build Application/Surl.Composition.UnitTests -warnaserror` is clean, the fast tests are green, no test
      needs `TestCategory=Integration`, and `Surl.Composition.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-816 (timed out after 120 min); reuses its serving wiring from stash `darkfactory BL-816 20261106-033059`.
