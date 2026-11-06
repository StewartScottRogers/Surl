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
completed: 2026-11-06
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

- [x] Named tests over `FakeListenerFactory` and `StartTestSeams` (no network): a server composed for `smtp` and
      `imap` publishes, on one subscription, the SMTP exchange's start and bytes, the mail store's delivery event,
      and the IMAP exchange that reads it, with each exchange's listen URL; two `ComposeAsync` calls give two
      streams that never see each other's events.
- [x] Named tests: `--fault` rules from the command line reach the plan in order; a drop rule on one listen URL
      drops it; a refuse-login rule refuses that account's login; the start-up warning is written at the level the
      ADR says, byte for byte; with no `--fault`, no warning and no behaviour change.
- [x] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean, the fast tests are green
      (including `Application/Surl.Console.UnitTests`, which also exercises the composition), and the library
      keeps 100% line and branch coverage.

## Notes

- Delivered (dark factory lane 1, 2026-11-05). `ComposeAsync` makes one `SurlEventStream` (on the seams'
  `TimeProvider`) and one `FaultPlan` (`FaultPlanComposition.Compose` over `SurlCommandLine.Faults`, in order,
  `f1`...) per start, carried as the internal `EventStreamAndFaultPlan` and handed to the content store, the
  message bus, the mail store, the object metadata index, `AuthenticationComposition.Compose` (new optional
  `events`/`faults`), `ComposedProtocolServices.Events`/`Faults` (init, null objects by default, so
  `ComposeUnservedProtocolServices` keeps them), `ExtendedProtocolServerServices.Events`/`Faults` and the
  `ServingEngine`'s ten-argument constructor. `ComposedServing.Events` (`SurlEventStream`, subscribe there) and
  `ComposedServing.Faults` (`FaultPlan`) expose them.
- Choices with a sensible default, taken here (ADR-0124 decisions already make the rest):
  - Warning text exactly as ADR-0124 decision 4 writes it, `rule(s)` literally, so the bytes need no amendment;
    written from the info level up (as ADR-0032 section 9's loosening warnings are), once, right after them.
  - Exposed as the concrete `SurlEventStream` and `FaultPlan`, not the interfaces, because the host needs
    `Subscribe` and `Add`/`Remove`/`Clear` (BL-806, BL-808, BL-816).
  - `LoadServiceStateAsync` and `LoadMessageBusAsync` gained required `events`/`faults` parameters (callers are
    this project only); `LoadMailStoreAsync`'s old overload stays (Surl.Console.UnitTests calls it) and passes
    the null objects; `ComposeContentStore` gained a four-argument overload; `ComposeObjectMetadataIndex` an
    optional `events`. The LDAP server takes neither: it has no store-change publishing today.
- Tests (Surl.Composition.UnitTests, 157): `SurlServerCompositionTests.EventsAndFaults.cs` (SMTP then IMAP on one
  subscription with listen URLs, the mail store's `Delivered` between them; two starts, two streams; rules in
  order; drop by listener; refuse-login by account; no rule, no change; the warning byte for byte, silenced by
  `-s`, absent without rules; extended services handed the same stream and plan) and `FaultPlanCompositionTests`.
  `FakeListenerFactory` gained `ConnectionsByScheme` and the project a `FakeConnection`, as Surl.Console.UnitTests has.
- Measured: Surl.Composition.UnitLibrary 100% line, 100% branch, worst CRAP 10 (`Measure-CodeQuality.ps1`);
  whole solution builds with 0 warnings; every fast test project green.
- 2026-11-06 (lane 1): restored the 2026-11-05 work from stash 'darkfactory sync 20261105-175401' (d4068484 and its untracked-files parent) rather than redoing it; Surl.Composition had not changed since its base. Rebuilt: 0 warnings; fast tests green (Surl.Composition.UnitTests 157, Surl.Console.UnitTests 398).

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-06: Doing -> Backlog. Lane 1 finished it on 2026-11-05 (build, tests, quality and format green; moved to Done) but was stopped before committing; the adopting lane stashed the work. Apply the stash 'darkfactory sync 20261105-175401' (find it with git stash list; 10 files in Surl.Composition and its tests), verify and push rather than redo it
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. Each ComposedServing owns one event stream and one fault plan built from --fault, handed to every store, the policy, the extended services and the engine, exposed as Events and Faults, with the --fault start-up warning
