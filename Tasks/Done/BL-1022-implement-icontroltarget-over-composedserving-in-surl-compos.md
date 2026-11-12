---
id: BL-1022
title: Implement IControlTarget over ComposedServing in Surl.Composition
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1016, BL-814, BL-805, BL-1035]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-06
completed: 2026-11-11
---
# BL-1022 — Implement IControlTarget over ComposedServing in Surl.Composition

## Goal

`Surl.Composition` references `Surl.Control` and has `ComposedControlTarget`, an `IControlTarget` over a composed
instance's event stream, fault plan and stores (`ComposedServing.Events`, `.Faults`, `.Stores`): adding, listing
and removing fault rules, seeding mail, writing and deleting files, publishing messages, adding and removing
accounts, and reset, snapshot and restore - each refusal mapped to its `ControlRefusalKind`. Nothing binds it yet
(BL-977 does).

## Context

- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md)
  decision 7 (`IControlTarget`, implemented by `Surl.Composition` over `ComposedServing`) and decision 3 (the
  store control BL-805 exposed).
- Built on: BL-814 (`Application/Surl.Control.UnitLibrary`: `IControlTarget`, `IControlSnapshot`,
  `ControlRefusalKind`), BL-805 (`ComposedStores`), BL-1016 (`ContentStore.DeleteFileForTheTest`).
- Split from BL-816 (timed out after 120 min, 2026-11-06), whose lane had this written and its 243 composition
  tests passing. Reuse from the stash whose message is `darkfactory BL-816 20261106-033059` (commit
  `0b6c48207fcfb912c5b903ddc55ed2a06d93e6da`, called `S`; find its index with `git stash list`):
  - untracked, with `git checkout "S^3" -- <path>`:
    `Application/Surl.Composition.UnitLibrary/ComposedControlTarget.cs` (175 lines),
    `Application/Surl.Composition.UnitLibrary/FaultItemText.cs` (77, writes a rule back in the `--fault` grammar
    for `GET /v1/faults`), `Application/Surl.Composition.UnitTests/ComposedControlTargetTests.cs`,
    `Application/Surl.Composition.UnitTests/FaultItemTextTests.cs`.
  - tracked, by `git diff "S^" "S" -- <file> | git apply` (none changed on the shared branch since):
    `Surl.Composition.UnitLibrary.csproj` (the `Surl.Control` reference), `ComposedSnapshot.cs`
    (implements `IControlSnapshot`), `ComposedStores.cs` (`DeleteFile`).
  - Leave `ComposedServing.cs`, `ComposedAuthentication.cs`, `SurlServerComposition.Start.cs`,
    `FakeListenerFactory.cs` and the `Control*` files to BL-972 and BL-977.
  - `Surl.Control` gained lines in `ControlEventKinds.cs`, `ControlEventWriter.cs` and `ControlJson.cs` on the
    shared branch after the stash; build, and adjust if a member moved.
- In `ComposedControlTargetTests`, do not pass `--allow-anonymous` (it makes every recipient deliverable, which
  hides the refusal mapping); the lane's last fix.
- Stand-in: this is the bridge from the control endpoint to the instance's shared stores, event stream and fault
  plan; a test seeds, resets and injects faults through it. It publishes what the stores already publish.

## Acceptance criteria

- [x] `ComposedControlTargetTests` exists and passes, including `AddFault_AnItem_AddsTheRuleUnderTheNextIdAndListFaultsWritesItBack`,
      `WriteFileAsyncAndDeleteFileAsync_AFile_WriteItThenDeleteIt`, `SeedMailAsync_AnInvalidAddressNoSuchAccountOrATooLargeMessage_IsRefusedByKind`
      and `ResetSnapshotAndRestore_InMemory_RestoreTheFileTheSnapshotHeld`.
- [x] `FaultItemTextTests` exists and passes, including `Format_AValueTheGrammarWouldSplit_PercentEncodesSeparatorsPercentAndControls`.
- [x] Every existing `Surl.Composition.UnitTests` test passes.
- [x] `dotnet build Application/Surl.Composition.UnitTests -warnaserror` is clean, the fast tests are green, and
      `Surl.Composition.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 6): restored the four BL-816 files and the three tracked diffs from stash commit `0b6c4820` cleanly (no conflicts), but `dotnet build` fails: global.json pins SDK 10.0.401 and only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed. Waits on BL-1035; the restored work is left uncommitted for the shift to stash.
- 2026-11-11 (lane 3): SDK 10.0.404 is now installed (rolls forward from 10.0.401). Restored the four untracked
  files with `git restore --source "S^3"` and the `.csproj` and `ComposedSnapshot.cs` diffs with `git apply`.
  Changes from the stash, each a default taken under the task's rules:
  - Dropped the stash's `ComposedStores.DeleteFile`: the shared branch already has `ComposedStores.DeleteFileAsync`
    (BL-954), so `ComposedControlTarget.DeleteFileAsync` uses it (`Done` is true) and `ComposedStores.cs` is unchanged.
  - `SeedMailAsync` now refuses `MailRecipientLookup.RelayingDenied` (a recipient outside every `--mail-domain`) as
    `NotFound`, like `NoSuchAccount` - ADR-0124 decision 7's 404 for an unknown account; the stash let it fall
    through to a `Conflict` naming a null delivery. Restructured as "return on Succeeded, else one switch on the
    lookup" so every branch is reachable.
  - `ReadEventsAsync` ends the read on cancellation by disposing the subscription (the channel completes and the
    loop ends) instead of throwing, as `IControlTarget` says ("until cancelled"); otherwise the loop's normal exit
    was unreachable and coverage stayed below 100%. `ControlEndpoint.FeedEventsAsync` handles either ending.
  - Not here: leaving the endpoint's own exchanges out of the read (`IControlTarget`'s summary) needs the binding,
    BL-977.
  - Tests added: `SeedMailAsync_ARecipientOutsideTheMailDomains_IsNotFound`,
    `ReadEventsAsync_AnAccountAddedThenCancelled_YieldsItsStoreChangeThenEnds`,
    `ReadEventsAsync_Cancelled_EndsTheReadWithoutThrowing`, and two `RefusalKindOf` rows. 353 composition tests pass;
    `ComposedControlTarget`, `FaultItemText` and `ComposedSnapshot` are at 100% line and branch in the composition
    tests alone (the library's older gaps there are the ones `Surl.Console.UnitTests` covers in the merged measure).

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-816 (timed out after 120 min); reuses its control target from stash `darkfactory BL-816 20261106-033059`.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing builds
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. ComposedControlTarget drives a composed instance's faults, stores and event stream as Surl.Control's IControlTarget, each refusal mapped to its ControlRefusalKind
