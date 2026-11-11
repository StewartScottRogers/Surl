---
id: BL-461
title: Serve webtransport from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-460, BL-405, BL-384]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-11
---
# BL-461 — Serve webtransport from the surl executable

## Goal

`surl webtransport://127.0.0.1:0` listens on UDP with QUIC and HTTP/3, as BL-384 does for `https`, and serves
WebTransport sessions. The `--aihelp` topic and help category exist, and the extension switch turns it off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`, using
  the QUIC listener and the certificate composition BL-384 added for HTTP/3.
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with `webtransport`, an
  `AiHelpProse.TopicAbout` paragraph naming the pinned WTH3 draft as its oracle, an `AiHelpExamples` entry, and
  the topic list in `AiHelpTextTests` growing by one.

## Acceptance criteria

- [x] A named test finds `webtransport` registered when the switch is on, and absent when it is off.
- [x] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

- 2026-11-05 (lane 4): The Context is out of date. BL-405 put extended-server registration in
  `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs` (`Registrations`), not in
  `Surl.Console/CommandLineRunner.cs`. Serving `webtransport` therefore needs a registration there, plus its
  on/off test in `Surl.Composition.UnitTests`, so both were added to `touches`.
  `Surl.Extended.Protocol.WebTransport.UnitLibrary` has no `IProtocolServer` yet, only the session, capsule
  and CONNECT pieces, so the registration must build one over the HTTP/3 serving BL-384 composed. BL-803
  (in Doing) touches `Surl.Composition`, so this task went back to Backlog until BL-803 is Done.

- 2026-11-11 (lane 3): Added the WebTransport library and its tests to `touches` (no task in Doing names them): `WebTransportProtocolServer` is the missing `IDatagramProtocolServer`. Decisions are ADR-0117 Amendment 2: certificate through a `QuicCertificateSource` filled by `ComposeServing`, every path accepted, `webtransport` needs `--cert` or `--self-signed`, and the 49.7-day head-timeout clamp the WebTransport timers lacked.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Needs Application/Surl.Composition.UnitLibrary and .UnitTests (ExtendedProtocolServers.Registrations), which BL-803 in Doing touches; retry once BL-803 is Done
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. webtransport served from surl: registration, QUIC certificate, aihelp topic, tests
