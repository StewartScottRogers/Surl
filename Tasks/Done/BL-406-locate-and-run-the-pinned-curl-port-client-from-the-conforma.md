---
id: BL-406
title: Locate and run the pinned Curl port client from the conformance tests
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-543]
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests, CurlPortBuilds.json]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-406 — Locate and run the pinned Curl port client from the conformance tests

## Goal

An integration test can ask for the Curl port's client, pinned as BL-543's ADR decision 2 says, and run
it against a live `surl`. It is refused when the binary does not match its pin. Nothing in the upstream-curl
locator accepts the port.

## Context

- Decision: BL-543's ADR, decisions 1 to 3. If the ADR names a different pin file than the planning
  name `CurlPortBuilds.json`, use the ADR's name and correct this task's `touches`.
- The model to follow, without merging the two: `Conformance/Surl.Conformance.UnitLibrary`'s `UpstreamCurlLocator`,
  `UpstreamCurlBuildPins`, `PinnedUpstreamCurlBuild`, `UpstreamCurlRunner`, `UpstreamCurlRunResult` and
  `UnpinnedUpstreamCurlException`, with `IUpstreamCurlFileAccess` as the file seam. The port gets its own types
  (planning names `CurlPortClientLocator`, `CurlPortClientPins`, `CurlPortClientRunner`), so an upstream-curl
  test can never be handed the port, and the reverse.
- `Conformance/Surl.Conformance.UnitTests`: `UpstreamCurlLocatorTests` and `UpstreamCurlBuildPinsTests` show
  how a locator is unit tested with `FakeUpstreamCurlFileAccess` and no real binary. `SurlOnLoopback` starts a
  live `surl`.
- The tests that run the port's client against a server are each server's own integration task. This task
  ships only the locator, the runner and the pin file.

## Acceptance criteria

- [x] The pin file exists with the fields the ADR names and at least one entry (or an empty `builds` list and
      a comment saying how an entry is added, if the ADR leaves pinning to a later step).
- [x] Named unit tests, with no real binary, show that: a matching binary is located; a binary whose SHA-256
      differs is refused with the port's own unpinned exception; a path listed in `UpstreamCurlBuilds.json` is
      never returned by the port's locator; and a missing binary marks the test inconclusive, as
      `UpstreamCurlUnavailability` does for upstream curl.
- [x] `UpstreamCurlBuildPinsTests` gains a test that no `UpstreamCurlBuilds.json` entry has the same SHA-256 as
      a `CurlPortBuilds.json` entry.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Conformance.UnitLibrary` keeps
      100% line and branch coverage.

## Notes

- ADR-0083 decision 2 names the pin file `CurlPortBuilds.json`, so `touches` stands. Types shipped:
  `PinnedCurlPortClient`, `CurlPortClientPins`, `CurlPortClientLocator`, `CurlPortClientLocation`,
  `CurlPortClientUnavailability`, `UnpinnedCurlPortClientException`, `CurlPortClientRunner`,
  `CurlPortClientRunResult`; the tests' helper is `PinnedCurlPortClientOnThisMachine`.
- Pin file ships with an empty `builds` list and a comment saying how to add an entry: this lane's
  permissions refused cloning the port and running `dotnet publish`, and pinning is an ordinary
  commit by ADR-0083. Filed BL-546 to publish and pin it.
- Choices: the port's locator reuses `IUpstreamCurlFileAccess` as its file seam, since it only
  checks for and reads a file; a second identical interface would add nothing. A pin's `path` may
  name `%NAME%` environment variables, expanded at location time (`Environment.ExpandEnvironmentVariables`),
  because ADR-0083's default folder sits under `%LOCALAPPDATA%`/`$HOME`, which differs per machine.
  Pins whose path an upstream pin lists are passed over (case-insensitive), and a file whose hash
  is an upstream pin's is refused even if the port pin says so - belt and braces with the new test.
- Tests: `CurlPortClientLocatorTests` (`Locate_PinnedFileMatches_ReturnsTheClient`,
  `Locate_Sha256Differs_ThrowsUnpinnedCurlPortClientException`,
  `Locate_PinnedPathIsListedInUpstreamCurlBuilds_NeverReturnsIt`,
  `RunnerFor_PinnedFileAbsent_MarksTheTestInconclusive`, ...), `CurlPortClientPinsTests`,
  `CurlPortClientRunnerTests`, `CurlPortClientRunResultTests`, `UnpinnedCurlPortClientExceptionTests`,
  and `UpstreamCurlBuildPinsTests.Parse_RepositoryPinFiles_ShareNoSha256WithCurlPortBuilds`
  (Integration, because it reads repository files, as the other pin-file tests do). Coverage of
  `Surl.Conformance.UnitLibrary`: line 1, branch 1 (cobertura, fast run).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Conformance tests can locate, verify by SHA-256 and run the Curl port's client from CurlPortBuilds.json, apart from upstream curl's pins
