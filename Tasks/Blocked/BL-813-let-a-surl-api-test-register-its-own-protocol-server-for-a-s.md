---
id: BL-813
title: Let a Surl.Api test register its own protocol server for a scheme
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-804, BL-811, BL-1035]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-813 — Let a Surl.Api test register its own protocol server for a scheme

## Goal

A .NET test that needs a protocol Surl does not serve (or a deliberately odd variant of one) registers its own
`IConnectionProtocolServer` or `IDatagramProtocolServer` for a scheme on `SurlServerOptions`, and it is composed
beside the built-in servers through BL-804's registration list, gets the same listener seam, limits, event stream
and fault plan, and shares the instance's stores if it asks for them.

## Context

- Decision: BL-822's ADR decision 2 (whether a test may replace a built-in scheme or only add a new one - planning
  default: add only; how a custom scheme's listen URL is parsed, since `Surl.Cli`'s `ListenUrlParser` refuses an
  unknown scheme today; which composed services the factory receives - planning: the content store, mail store,
  message bus and authentication policy).
- Built on BL-804 (`ProtocolServerRegistration` or the ADR's name). Explicit registration only (a factory
  delegate), AOT-safe, no scanning.
- Where: `Application/Surl.Api` (`SurlServerOptions`, `SurlServer`, `SurlServerHostSeams`). If the ADR requires
  `Surl.Cli` or `Surl.Composition` changes to accept the scheme, file them as tasks this one depends on and move
  this task to `Blocked` naming them rather than widening it.
- Stand-in: open for extension (protocols). Its exchanges appear on the event stream through the engine (BL-787)
  with no work in the test's server.
- Amended by ADR-0124 decision 2: add only (a scheme any original or extended server claims, turned on or not, is
  `ArgumentException` at `StartAsync`); `SurlServerOptions.ProtocolServers` takes `SurlProtocolServer(Scheme,
  Func<SurlProtocolServices, IProtocolServer> Create)`; a custom scheme's listen URL must name its port (0
  allowed), and `Surl.Api` takes it out of `ListenUrls` before `Surl.Cli` parses the rest and reads it with
  `System.Uri` into a `ListenUrl` - no `Surl.Cli` change - handing it to BL-804's `AdditionalProtocolServers`
  input. It already depends on BL-804 for that input; no other task is needed, so this task is not Blocked.

## Acceptance criteria

- [x] Named tests (fake listeners and connections): a test server registered for scheme `echo` answers on its
      listen URL beside `http`; its exchange is recorded with its listen URL; a drop fault on its listen URL drops
      it; registering a built-in scheme is refused as the ADR says.
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green, and `Surl.Api` keeps
      100% line and branch coverage.

## Notes

- 2026-11-11 (lane 3): code, tests and docs written; left uncommitted for the shift to stash, since the pinned
  SDK 10.0.401 is not installed (BL-1035) and `dotnet build` refuses to start. Verified with SDK 11.0.100 run from a
  directory outside the repository (so `global.json` is not read): `Surl.Api` and `Surl.Mcp.UnitTests` build with
  `-warnaserror`, the 242 `Surl.Api.UnitTests` pass, and `Surl.Api` is at 100% line and 100% branch coverage. The
  next run only re-verifies with the pinned SDK, ticks the second box and commits.
- Design (ADR-0124 decision 2, no new ADR needed): `SurlProtocolServer(Scheme, Create)`, `SurlProtocolServices`
  (content store, mail store, message bus, `AuthenticationPolicy`, `TimeProvider`), and `CustomProtocolServers`, which
  refuses built-in (`SchemeDefaultPorts`, `ExtendedSchemeDefaultPorts`, both registration lists), duplicate and
  non-URI schemes, takes custom listen URLs out before `Surl.Cli` parses, reads them with `System.Uri`, and hands
  `AdditionalProtocolServers` to the composition. `BoundUrls` keeps the order given (the composition serves custom
  URLs last). Tests: `SurlServerTests.ProtocolServers.cs`.
- Choice: `surl`'s `--fault` reader refuses a listener or scheme it does not know, and `Surl.Cli` is BL-1028's.
  So `Surl.Api` reads a custom-scoped rule with a stand-in (`http://127.0.0.1:0`, `http`) and puts its own
  listener and scheme back on the rule; an `error` rule scoped to a custom scheme is refused (no reply shape).
- Choice: when every listen URL is custom, `surl` parses the stand-in `http://127.0.0.1:0` (it refuses a command
  line without one) and the parsed `ListenUrls` is emptied, so only the custom server is served.

- 2026-11-12: The finished work is recovered on branch `origin/recover/BL-813` (commit ff906f39, from the 2026-11-11 08:07 shift stash; it was 608 commits behind). Do not use `git stash`. In your worktree run `git merge --no-commit origin/recover/BL-813` or cherry-pick it, settle the conflicts in `Application/Surl.Api/CLAUDE.md`, `SurlServer.cs` and `SurlServerArguments.cs` (other lanes changed them since), then rebuild on SDK 10.0.404 and rerun the Surl.Api tests before moving the task to Done.

## Log

- 2026-11-05: Created.
- 2026-11-05: Amended by ADR-0124 (BL-822): custom listen URLs read by `Surl.Api`, composition input from BL-804.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run; code is done and verified on SDK 11, left in the worktree
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Backlog. Lane 3's finished code for this task is in a shift stash, not in this worktree, and I was denied access to git stash; SDK 10.0.404 is now installed so BL-1035's blocker is gone. Next run should restore the stash, re-verify, tick box 2 and commit.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Blocked. Stewart: allow lane 3 to read git stash (git stash list / apply) so the finished BL-813 code in the shift stash can be restored and re-verified on SDK 10.0.404; it was denied twice.
- 2026-11-12: Blocked -> Backlog. Stewart asked for the stashes to be recovered on 2026-11-12: the finished work is on origin/recover/BL-813; a lane merges it, see Notes.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Blocked. Stewart: allow lane 1 to run git merge --no-commit (or cherry-pick) of origin/recover/BL-813 in the lane worktree; git fetch and git merge were denied, so the recovered work cannot be applied.
