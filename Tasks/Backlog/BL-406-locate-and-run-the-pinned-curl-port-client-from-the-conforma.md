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
completed:
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

- [ ] The pin file exists with the fields the ADR names and at least one entry (or an empty `builds` list and
      a comment saying how an entry is added, if the ADR leaves pinning to a later step).
- [ ] Named unit tests, with no real binary, show that: a matching binary is located; a binary whose SHA-256
      differs is refused with the port's own unpinned exception; a path listed in `UpstreamCurlBuilds.json` is
      never returned by the port's locator; and a missing binary marks the test inconclusive, as
      `UpstreamCurlUnavailability` does for upstream curl.
- [ ] `UpstreamCurlBuildPinsTests` gains a test that no `UpstreamCurlBuilds.json` entry has the same SHA-256 as
      a `CurlPortBuilds.json` entry.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Conformance.UnitLibrary` keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
