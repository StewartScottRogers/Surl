---
id: BL-492
title: Serve redis and rediss from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-491, BL-405, BL-1035]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Documentation/Planning/Decisions/ADR-0106-how-the-redis-server-answers-redis-and-rediss.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-492 — Serve redis and rediss from the surl executable

## Goal

`surl redis://127.0.0.1:0` and `surl rediss://127.0.0.1:0` (TLS) start the Redis server, with its keyspace loaded
after the data-directory lock if it persists. The `--aihelp` topic and help category exist, and the extension
switch turns both off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`.
  `rediss` is wrapped for TLS (`ImplicitTlsSchemeServer`, ADR-0010). State kept across connections is loaded as
  `ServiceState` loads the mail store.
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with both schemes, an
  `AiHelpProse.TopicAbout` paragraph naming the RESP specification as its oracle, an `AiHelpExamples` entry, and
  the topic list in `AiHelpTextTests` growing by one.

## Acceptance criteria

- [ ] A named test finds `redis` and `rediss` registered when the switch is on, and absent when it is off.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

- 2026-11-05 (lane 4): extended servers are composed in
  `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs` (`Registrations`), not in
  `CommandLineRunner.cs` - the composition moved there (BL-804), and BL-421 registered `s3` the same way,
  touching Surl.Composition and its tests. Redis needs a `Registrations` entry, a project reference from
  Surl.Composition to `Surl.Extended.Protocol.Redis.UnitLibrary`, its keyspace loaded in `ServiceState`, and a
  test in `ExtendedProtocolServersTests`. So `touches` now names Surl.Composition.UnitLibrary and .UnitTests;
  BL-803 (in Doing) holds them, so this task went back to Backlog until they no longer overlap.
- 2026-11-11 (lane 2): code written but unbuilt - `dotnet build` fails because global.json pins
  SDK 10.0.401, which is not installed (BL-1035, Stewart's), so it went back to Backlog behind
  BL-1035; the shift stashes the uncommitted work. What was written, to pick up or redo:
  - `ExtendedProtocolServers.Registrations` gains `new(["redis", "rediss"], services => new
    RedisServer(services.AuthenticationPolicyFor("redis"), services.MessageBus))` between nats and
    s3, and Surl.Composition references `Surl.Extended.Protocol.Redis.UnitLibrary`. One server and
    one keyspace for both schemes; the keyspace is memory only (ADR-0106 decision 3), so nothing is
    loaded in `ServiceState` - the Goal's "if it persists" does not apply.
  - `rediss` TLS from the first byte without editing `TlsSchemes` (Surl.Protocol.Abstractions,
    held by BL-871): new `Surl.Cli.ExtendedImplicitTlsSchemes` ({rediss}); `ListenUrlParser` sets
    `IsImplicitTls = true` on such a listen URL (the engine already reads `ListenUrl.IsImplicitTls`,
    BL-1020), and `ServerTlsComposition.RequiresCertificate`/`NeedsTls` now take the `ListenUrl` and
    read `IsImplicitTls`, so `rediss` without `--cert`/`--self-signed` is `(58)`. No
    `ImplicitTlsSchemeServer` wrapper: `RedisServer.Schemes` already claims `rediss`, as `s3` needs
    none. ADR-0106 decision 7 still says "wrapped in `ImplicitTlsSchemeServer`" and needs an
    amendment saying this (added to `touches`), "Decided by Claude under Stewart's delegation".
  - Surl.Cli: `HelpCategories` row `redis` ("Redis and Rediss protocol (extended)"), the `redis`
    category added to `--head-timeout`, `--max-line`, `--max-filesize`, `-u`, `--user-file`,
    `--allow-anonymous` and `--allow-plaintext-auth`; `AiHelpProse` `redis` topic (RESP spec as
    oracle) and the `extended` paragraph now "four extended protocol servers"; `AiHelpExamples`
    `redis` entry; `ManualText` "REDIS OPTIONS" section.
  - Still to do: update the pinned texts in Surl.Cli.UnitTests (AiHelpTextTests topic list and
    table, AiHelpFactsTests, AiHelpExamplesTests, HelpTextTests category wraps, ManualTextTests,
    ListenUrlParserTests for `rediss` IsImplicitTls), Surl.Composition.UnitTests
    (ExtendedProtocolServersTests registration list, a `rediss` (58) test in
    ServerTlsCompositionTests), Surl.Console.UnitTests (CommandLineRunnerExtendedProtocolTests
    scheme lists and a redis/rediss on/off test, CommandLineRunnerTests `--version`), then build,
    fast tests and coverage.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Needs Application/Surl.Composition.UnitLibrary and .UnitTests (where extended servers are registered), which BL-803 in Doing touches
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: global.json pins .NET SDK 10.0.401, which is not installed, so nothing builds; code written and described under Notes
