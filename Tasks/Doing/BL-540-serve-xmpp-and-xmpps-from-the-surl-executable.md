---
id: BL-540
title: Serve xmpp and xmpps from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-539, BL-405]
touches: [Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-540 — Serve xmpp and xmpps from the surl executable

## Goal

`surl xmpp://127.0.0.1:0` starts the XMPP server with STARTTLS available when a certificate is configured, and
`surl xmpps://127.0.0.1:0` starts it with direct TLS (XEP-0368). The `--aihelp` topic and help category exist, and
the extension switch turns both off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`. TLS
  upgrade availability is passed as for the mail servers. `xmpps` is wrapped with `ImplicitTlsSchemeServer`
  (ADR-0010). XEP-0368 section 3 names ALPN `xmpp-client`; offer it on `xmpps` if the per-scheme ALPN list allows,
  or record why not in the task's Notes.
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with both schemes, an
  `AiHelpProse.TopicAbout` paragraph naming RFC 6120 as its oracle, an `AiHelpExamples` entry, and the topic list
  in `AiHelpTextTests` growing by one.

## Acceptance criteria

- [ ] A named test finds `xmpp` and `xmpps` registered when the switch is on, and absent when it is off.
- [ ] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

- 2026-11-05 (lane 4): Extended-server composition has moved out of `Surl.Console`'s `CommandLineRunner` into
  `Application/Surl.Composition.UnitLibrary`: an extended server joins as one entry in
  `ExtendedProtocolServers.Registrations` (ADR-0124, decision 2), and the switch test belongs in
  `Surl.Composition.UnitTests`. So this task touches both projects too. BL-803 (Doing) touches
  `Application/Surl.Composition.UnitLibrary` and `Application/Surl.Composition.UnitTests`, so the task goes back to
  Backlog until BL-803 frees them. `ExtendedSchemeDefaultPorts` in `Surl.Cli` already lists `xmpp` 5222 and
  `xmpps` 5223. The XMPP server lives in `Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary`.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Needs Application/Surl.Composition.UnitLibrary and .UnitTests, which BL-803 in Doing touches
- 2026-11-11: Backlog -> Doing.
