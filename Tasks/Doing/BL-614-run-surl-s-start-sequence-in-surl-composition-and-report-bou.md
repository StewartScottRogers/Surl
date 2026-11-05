---
id: BL-614
title: Run surl's start sequence in Surl.Composition and report bound listen URLs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-613]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-614 — Run surl's start sequence in Surl.Composition and report bound listen URLs

## Goal

`Surl.Composition` runs the whole ordered start of a parsed `SurlCommandLine` - option refusals, the data directory,
the content store, listen-URL refusals, authentication and host keys, the lock, the service state, TLS, the serving
engine - and answers either a start failure (`SurlExitCode` and the text after `surl: `) or a composed serving that
serves until cancelled and reports each listener's bound `ListenUrl` once all have bound. `CommandLineRunner` keeps
only parsing, help, log streams, writing and exit codes, and `surl` behaves exactly as before.

## Context

- Decision: BL-611's ADR, decision 2 (the composition's input and output, and the names of its types). BL-609 and
  BL-613 have moved the pieces; this task moves the sequence that calls them.
- The sequence today, in `Application/Surl.Console/CommandLineRunner.cs`: `ServeAsync` -> `FindOptionRefusal` ->
  `ServeAvailableAsync` (probe, `ComposeContentFileSystem`, `ComposeContentStore`, `FindListenUrlRefusal`,
  `ComposeAuthentication`) -> `LockThenServeAsync` -> `LoadServiceStateAndServeAsync` -> `ServeUnderTheLockAsync`
  (`ServerTlsComposition.Compose`) -> `ServeSecuredAsAskedAsync` (log streams and start-up warnings) ->
  `ServeLoggedAsync` (`ListenerStartReporter`, `ServingEngine`, `ComposeConnectionLimits`, bind failures and
  `(125) Internal error`). The runner's constructor seams (`createListenerFactory`, `canOpenDataDirectory`,
  `takeDataDirectoryLock`, `timeProvider`, `dataDirectoryFileSystem`, `readStartFile`, `createDataConnectionOpener`,
  `environmentVariables`, `composeExtendedProtocolServers`) become the composition's inputs.
- The log streams stay in `Surl.Console` (they open `--log-file` and the trace file); the composition takes the
  exchange log factory and the writer for start-up warnings from its caller, so `Surl.Api` can give its own.
- `ListenerStartReporter` already sees each bound `ListenUrl` (an `IListenerFactory` listener carries its bound
  port); the bound-URL report is what `Surl.Api`'s `BoundUrls` will read, while `surl` keeps writing the status lines
  through `ListenerStatusLine`.
- Every exit code and message `surl` writes is pinned by `Surl.Console.UnitTests`' `CommandLineRunner*Tests` and the
  `Integration` conformance tests; none may change.

## Acceptance criteria

- [ ] `Surl.Composition.UnitLibrary` has the start type the ADR names, taking a `SurlCommandLine` and the seams
      above, and `CommandLineRunner` calls it; `CommandLineRunner` no longer constructs `ServingEngine`.
- [ ] Named tests in `Surl.Composition.UnitTests`, with a fake listener factory and no network, show: a start
      on `http://127.0.0.1:0` reporting the bound URL with the fake's port once every listener is bound; an `https`
      URL with no certificate failing with `SurlExitCode.CertificateProblem` (58) and the text `surl` writes; an
      `sftp` URL without a host key failing with `SurlExitCode.FailedInit` (2); a second start on a locked data
      directory failing with exit code 124; cancellation ending a started serving with `SurlExitCode.Ok`.
- [ ] Every `CommandLineRunner*Tests` and `ProgramTests` test in `Surl.Console.UnitTests` passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary` and
      `Surl.Console` have 100% line and branch coverage.
- [ ] `Application/Surl.Composition.UnitLibrary/CLAUDE.md` and `Application/Surl.Console/CLAUDE.md` say which project
      runs each step of the start.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
