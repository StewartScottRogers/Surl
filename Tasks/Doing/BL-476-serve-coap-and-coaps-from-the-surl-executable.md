---
id: BL-476
title: Serve coap and coaps from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-475, BL-405]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-476 — Serve coap and coaps from the surl executable

## Goal

`surl coap://127.0.0.1:0` and `surl coaps://127.0.0.1:0` listen on UDP and serve CoAP, with DTLS credentials from
`--cert`/`--key` and the accounts file as BL-470's ADR says. The `--aihelp` topic and help category exist,
and the extension switch turns both off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`. Datagram
  servers are composed as `TftpProtocolServer` is.
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with both schemes, an
  `AiHelpProse.TopicAbout` paragraph naming RFC 7252, RFC 7959 and RFC 7641 as its oracle, an `AiHelpExamples`
  entry, and the topic list in `AiHelpTextTests` growing by one.

## Acceptance criteria

- [ ] A named test finds `coap` and `coaps` registered when the switch is on, and absent when it is off.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

- 2026-11-05 (lane 4): Extended-server composition has moved out of `Surl.Console`'s `CommandLineRunner` into
  `Application/Surl.Composition.UnitLibrary`: an extended server joins as one entry in
  `ExtendedProtocolServers.Registrations` (ADR-0124, decision 2), and the switch test belongs in
  `Surl.Composition.UnitTests`. So this task touches both projects too. BL-803 (Doing) touches
  `Application/Surl.Composition.UnitLibrary` and `Application/Surl.Composition.UnitTests`, so the task goes back to
  Backlog until BL-803 frees them. `ExtendedSchemeDefaultPorts` in `Surl.Cli` already lists `coap` 5683 and
  `coaps` 5684. The CoAP server lives in `Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary`.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Needs Application/Surl.Composition.UnitLibrary and .UnitTests, which BL-803 in Doing touches
- 2026-11-11: Backlog -> Doing.
