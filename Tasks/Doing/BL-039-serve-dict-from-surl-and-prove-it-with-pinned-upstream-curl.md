---
id: BL-039
title: Serve dict from surl and prove it with pinned upstream curl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-033, BL-020, BL-081]
touches: [Surl.Console, Surl.Console.UnitTests, Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-039 — Serve dict from surl and prove it with pinned upstream curl

## Goal

`surl dict://127.0.0.1:<port>/` serves DICT with the server from BL-033, and integration
tests prove that the pinned upstream curl 8.21.0 build gets the replies and exit codes
BL-033's recordings predict.

## Context

- BL-033's DICT server and its fixtures in `Surl.Protocol.Dict.UnitTests/Fixtures/`
  hold each case's command line and expected result.
- Register the server for `dict` in `Surl.Console`'s explicit composition, as BL-019 did
  for `http`, and add a `ProjectReference` from `Surl.Console` only if it is missing.
  The csproj already references every protocol server.
- BL-020's process runner, in-process `surl` start and `Assert.Inconclusive` rule are
  reused.

## Acceptance criteria

- [x] `Surl.Console` registers the DICT server for `dict`. A fast test in
      `Surl.Console.UnitTests` proves a `dict://` listen URL starts a listener with it.
- [ ] `[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` run the pinned
      build for each case BL-033 recorded, against a live `surl`, and assert its exit
      code and stdout equal the recorded ones.
- [ ] On Windows with the pinned build present,
      `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in `surl`.
- [ ] Any disagreement with the pinned build is fixed in `Surl.Protocol.Dict` through a
      new task, never by changing the expected result. Such tasks are listed in the Log.

## Notes

- 2026-09-28, first run (dark factory lane 3). The work was written and left uncommitted
  for the shift to stash; redo or restore it next run:
  - `CommandLineRunner.ComposeProtocolServers` adds `new DictProtocolServer(contentStore)`;
    `--version` now ends `Protocols: dict http`. `Surl.Console/CLAUDE.md` says so.
  - `FakeListenerFactory.ConnectionToAccept` hands one connection to the first accept, and
    `CommandLineRunnerTests.RunAsync_DictListenUrl_StartsAListenerTheDictServerAnswers`
    feeds `CLIENT libcurl 8.21.0` / `QUIT` through it and asserts the banner, `250 ok`,
    `221 bye`. Green, so the first box is ticked.
  - `Surl.Conformance.UnitTests` embeds BL-033's fixtures from
    `..\Surl.Protocol.Dict.UnitTests\Fixtures\*\*` as `DictFixtures/<case>/<file>` (read
    where they were recorded rather than copied), `SurlOnLoopback.StartAsync(scheme, …)` and
    `UrlWithPath` (a relative `Uri` would read `d:hello` as a scheme), the curl run moved
    into `PinnedUpstreamCurlInvocation` shared with the HTTP tests, and
    `UpstreamCurlQueriesSurlOverDictTests` runs the five recorded cases with `-sS`.
- Result against pinned curl 8.21.0 (win-x64): `define-hello`, `bare-hello`,
  `define-missing` and `show-db` match their recordings; `match-hel` does not - surl
  answers `552 no match` because `MATCH` lists through `ContentStore.ListDirectory`, which
  is off without `--list-directories`, against ADR-0011 section 4. Per the last criterion
  the expected result stays and the fix is BL-081 in `Surl.Protocol.Dict` (and possibly
  `Surl.Content`); this task waits on it.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Pinned upstream curl disagrees on `match-hel`; filed BL-081 to fix it in Surl.Protocol.Dict.
- 2026-09-28: Doing -> Backlog. Waits on BL-081: pinned upstream curl gets 552 for match-hel because Surl.Protocol.Dict's MATCH is gated by --list-directories
- 2026-09-29: Backlog -> Doing.
