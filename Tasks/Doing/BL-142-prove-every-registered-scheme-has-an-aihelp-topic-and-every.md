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
completed:
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

- [ ] A test (named as the ADR says) asserts every scheme of every registered server is
      claimed by exactly one `--aihelp` protocol topic, and every protocol topic's schemes are
      all registered; it fails if a server is added to `ComposeProtocolServers` without a topic
      (shown in Notes by temporarily removing one topic and seeing it fail).
- [ ] A test runs every example the generator exposes and asserts the output and error lines
      and the exit code match what the example shows; the `Listening on` example (port 0),
      the `--directory` example and the `(124)` data-directory-in-use example are each covered.
- [ ] `dotnet build Surl.Console -warnaserror` is clean; `dotnet test Surl.Console.UnitTests
      --filter "TestCategory!=Integration"` passes on the fast filter with no network; the
      tests use no Windows-only path (a temporary directory from `Path.GetTempPath()`);
      `Surl.Console` stays at 100% line and branch coverage.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
