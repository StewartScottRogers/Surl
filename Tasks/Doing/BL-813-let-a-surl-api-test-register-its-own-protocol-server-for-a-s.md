---
id: BL-813
title: Let a Surl.Api test register its own protocol server for a scheme
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-804, BL-811]
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

- [ ] Named tests (fake listeners and connections): a test server registered for scheme `echo` answers on its
      listen URL beside `http`; its exchange is recorded with its listen URL; a drop fault on its listen URL drops
      it; registering a built-in scheme is refused as the ADR says.
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean, the fast tests are green, and `Surl.Api` keeps
      100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Amended by ADR-0124 (BL-822): custom listen URLs read by `Surl.Api`, composition input from BL-804.
- 2026-11-11: Backlog -> Doing.
