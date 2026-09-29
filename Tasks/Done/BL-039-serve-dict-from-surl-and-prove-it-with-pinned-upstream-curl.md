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
completed: 2026-09-29
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
- [x] `[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` run the pinned
      build for each case BL-033 recorded, against a live `surl`, and assert its exit
      code and stdout equal the recorded ones.
- [x] On Windows with the pinned build present,
      `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and
      `Measure-CodeQuality.ps1` reports no failing member in `surl`.
- [x] Any disagreement with the pinned build is fixed in `Surl.Protocol.Dict` through a
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
- 2026-09-29, second run (dark factory lane 2), after BL-081. The first run's stash was
  not restored; the work was redone smaller, following BL-040's Gopher pattern that had
  landed meanwhile:
  - `CommandLineRunner.ComposeProtocolServers` adds `new DictProtocolServer(contentStore)`;
    `--version` now ends `Protocols: dict gopher http https mqtt telnet tftp`, and
    `Surl.Console/CLAUDE.md` names the DICT server.
  - `CommandLineRunnerTests.RunAsync_DictListenUrl_StartsADictListenerAndReturnsOkWhenCancelled`
    proves a `dict://` URL starts a listener (an unregistered scheme is refused before
    any bind). Choice: the Gopher-style listener test rather than the first run's
    fed-connection test, because the DICT replies themselves are proven end to end by
    the conformance tests below, and it needs no change to `FakeListenerFactory`.
  - `UpstreamCurlQueriesSurlOverDictTests` runs the five recorded cases with `-sS`
    against a fresh in-process surl serving `hello`, `help` and `world`. Choice: it reads
    each fixture's `exitcode.txt` and `stdout.bin` from `Surl.Protocol.Dict.UnitTests/Fixtures`
    under the repository root at run time (as `PinnedUpstreamCurl` already reads
    `UpstreamCurlBuilds.json`), rather than embedding them, so the csproj is unchanged.
    A fresh surl's first exchange has id 1, the id in the recorded banners.
- Result against pinned curl 8.21.0 (win-x64): all five cases, `match-hel` included,
  match their recordings. `dotnet test --filter "FullyQualifiedName~Surl.Conformance"`:
  91 passed. No new disagreement, so no new task.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Pinned upstream curl disagrees on `match-hel`; filed BL-081 to fix it in Surl.Protocol.Dict.
- 2026-09-28: Doing -> Backlog. Waits on BL-081: pinned upstream curl gets 552 for match-hel because Surl.Protocol.Dict's MATCH is gated by --list-directories
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. surl dict:// serves DICT, and pinned upstream curl 8.21.0 gets the recorded exit code and stdout for all five BL-033 cases
