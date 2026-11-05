---
id: BL-803
title: Compose one event stream and fault plan per server in Surl.Composition and hand them to every store, login check and server
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-785, BL-787, BL-788, BL-789, BL-790, BL-791, BL-792]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-803 — Compose one event stream and fault plan per server in Surl.Composition and hand them to every store, login check and server

## Goal

Each `ComposedServing` - one `surl` run, one `Surl.Api` server - owns exactly one event stream and one fault plan,
built from `SurlCommandLine.Faults` and handed to the serving engine, the content store, the mail store, the
message bus, the authentication policy and the extended servers' services, and exposes both to its host, so every
exchange and store change of that instance appears on one stream and every `--fault` rule takes effect.

## Context

- Decision: BL-822's ADR decisions 1 and 4 (how a host subscribes; the `--fault` start-up warning text, written
  like the other loosening warnings).
- Built on: BL-787 (`SurlEventStream`, the engine publishing), BL-792 (`FaultPlan`, engine faults), BL-788 to
  BL-791 (stores and policy taking a publisher and plan), BL-785 (`SurlCommandLine.Faults`).
- Where: `Application/Surl.Composition.UnitLibrary` - read its `CLAUDE.md`. `SurlServerComposition.ComposeAsync`
  creates the stream and plan (never static: ADR-0099 decision 8), passes them into `ComposeContentStore`,
  `LoadServiceStateAsync` (mail store, bus), `AuthenticationComposition` and `ServingEngine`, and adds them to
  `ExtendedProtocolServerServices`. `ComposedServing` exposes them (planning names `Events`, a subscribe point,
  and `Faults`, the plan) so `Surl.Api` (BL-806, BL-808) and the control endpoint (BL-816) reach them. The warning
  goes through `ComposedServing.ServeAsync`'s start-up warnings to `Log`, as ADR-0099 decision 2 says, so its bytes
  match between `surl` and `Surl.Api`.
- `ComposeUnservedProtocolServers` (schemes only) gets null objects; nothing is served through it.
- Keep `ComposeProtocolServers`'s name (ADR-0099 decision 1; many tasks name it). Servers receive the plan and
  publisher through `ExchangeContext` (set by the engine), so no protocol server constructor changes here.
- Stand-in: this is the one publish point per instance (observable) and the one fault plan (fault-injectable).
  Cross-protocol proof that one stream sees SMTP and IMAP exchanges and the mail store's change is in Acceptance.

## Acceptance criteria

- [ ] Named tests over `FakeListenerFactory` and `StartTestSeams` (no network): a server composed for `smtp` and
      `imap` publishes, on one subscription, the SMTP exchange's start and bytes, the mail store's delivery event,
      and the IMAP exchange that reads it, with each exchange's listen URL; two `ComposeAsync` calls give two
      streams that never see each other's events.
- [ ] Named tests: `--fault` rules from the command line reach the plan in order; a drop rule on one listen URL
      drops it; a refuse-login rule refuses that account's login; the start-up warning is written at the level the
      ADR says, byte for byte; with no `--fault`, no warning and no behaviour change.
- [ ] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean, the fast tests are green
      (including `Application/Surl.Console.UnitTests`, which also exercises the composition), and the library
      keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
