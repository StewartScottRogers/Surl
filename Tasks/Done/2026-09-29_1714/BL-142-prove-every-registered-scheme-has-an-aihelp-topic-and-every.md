---
id: BL-142
title: Prove every registered scheme has an --aihelp topic and every --aihelp example prints what surl prints
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-141]
touches: [Surl.Console, Surl.Console.UnitTests]
requirement: FR-035
created: 2026-09-29
completed: 2026-09-29
---
# BL-142 — Prove every registered scheme has an --aihelp topic and every --aihelp example prints what surl prints

## Goal

`Surl.Console.UnitTests` fails when a protocol server registered in `Surl.Console` has no
`--aihelp` topic, when a protocol topic names a scheme no registered server claims, or when
an `--aihelp` example's shown output differs from what `CommandLineRunner` writes for that
command line - the two checks BL-137's ADR places in `Surl.Console.UnitTests`.

## Context

- Decision: BL-137's ADR (expected ADR-0046), decisions 3 (a protocol category is a protocol
  topic), 6 (how a topic names its schemes) and 7, 9 (how examples are proved; which tests).
- Registered servers: `Surl.Console/CommandLineRunner.cs` `ComposeProtocolServers` and
  `ComposeUnservedProtocolServers` (schemes `http`, `https`, `dict`, `gopher`, `gophers`,
  `mqtt`, `mqtts`, `telnet`, `tftp` on 2026-09-29; `CommandLineRunnerTests`'
  `RunAsync_Version_...` test already reads them through `--version`). Reading the schemes
  from a test may need an `internal` seam in `Surl.Console` (`InternalsVisibleTo
  Surl.Console.UnitTests` exists); add the smallest one, covered.
- Examples: run each example's arguments through `CommandLineRunner.RunAsync` with
  `Surl.Console.UnitTests/FakeListenerFactory.cs` (bound port `FakeListenerFactory.BoundPort`,
  49731), `FakeLockHolder.cs` for the data-directory lock case and a temporary directory for
  `--directory`, and compare the written lines with the example's shown output, the bound port
  substituted as the ADR's decision 7 says. No socket is opened (NFR-001).
- If a check finds a real mismatch in the text, fix the text in `Surl.Cli.UnitLibrary` in a
  follow-up task filed by `task-planner`, and block this one on it; do not widen this task.

## Acceptance criteria

- [x] A test (named as the ADR says) asserts every scheme of every registered server is
      claimed by exactly one `--aihelp` protocol topic, and every protocol topic's schemes are
      all registered; it fails if a server is added to `ComposeProtocolServers` without a topic
      (shown in Notes by temporarily removing one topic and seeing it fail).
- [x] A test runs every example the generator exposes and asserts the output and error lines
      and the exit code match what the example shows; the `Listening on` example (port 0),
      the `--directory` example and the `(124)` data-directory-in-use example are each covered.
- [x] `dotnet build Surl.Console -warnaserror` is clean; `dotnet test Surl.Console.UnitTests
      --filter "TestCategory!=Integration"` passes on the fast filter with no network; the
      tests use no Windows-only path (a temporary directory from `Path.GetTempPath()`);
      `Surl.Console` stays at 100% line and branch coverage.

## Notes

- Plan, as delivered: `Surl.Console.UnitTests/CommandLineRunnerAiHelpTests.cs` holds ADR-0046
  decision 9's three tests: `RegisteredSchemes_AreEachClaimedByExactlyOneProtocolTopic`,
  `ProtocolTopics_ClaimOnlyRegisteredSchemes` and `RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows`
  (`[DynamicData]`, one row per `AiHelpExamples.All` entry, keyed by topic and title - all 19
  pass, among them the port-0 `Listening on` examples, `--directory <path>` and the `(124)` one).
- Seam: `internal CommandLineRunner.ComposeRegisteredSchemes()`, extracted from the `--version`
  text so `--version` and the tests read one list; covered by the `--version` test and the new
  ones. Chosen over parsing `--version`'s `Protocols:` line, which would tie the check to that
  line's layout.
- Example runs: `<path>` is a new directory under `Path.GetTempPath()` (deleted afterwards),
  opened with the real `DataDirectoryProbe.CanOpen`; the lock delegate answers
  `DataDirectoryLockOutcome.InUse(<path>)` for `DataDirectoryHeldByAnotherSurl`, else
  `Taken(new FakeLockHolder())`, so no `.surl/lock` is written; a serving example is
  cancelled once `FakeListenerFactory.AcceptStarted` completes. No socket opened.
- Mutation shown: with the `telnet` row removed from `Surl.Cli.UnitLibrary/HelpCategories.cs`,
  `RegisteredSchemes_AreEachClaimedByExactlyOneProtocolTopic` failed
  (`Assert.HasCount(1, claimingTopics)`); the row was restored, `Surl.Cli` is unchanged.
- No text mismatch found, so no follow-up task. `Measure-CodeQuality.ps1 -Library Surl.Console`:
  100% line, 100% branch, 0 failing members, worst CRAP 10. `dotnet format --verify-no-changes`
  reports only ENDOFLINE on files this task did not change (checkout line endings).

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Surl.Console.UnitTests fails when a registered scheme lacks an --aihelp topic, a topic names an unregistered scheme, or an example's shown output differs from RunAsync
