---
id: BL-1886
title: Name the AMQP servers' SASL logins in the --auth help, manual and AI help text
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1886 — Name the AMQP servers' SASL logins in the --auth help, manual and AI help text

## Goal

The `--auth` option page, manual paragraph and AI help paragraph say that `--auth` also sets the SASL mechanisms of the AMQP 0-9-1 and AMQP 1.0 servers, where that is true of the code.

## Context

- Found by BL-1883, by reading the code.
- `CommandLineOptions.AuthExplanation`, the `--auth` paragraph in `ManualText.cs` and the one in `AiHelpProse.cs` name SASL mechanisms "for SMTP, IMAP and POP3 logins" only. Both AMQP servers offer what `ISaslAuthenticationPolicy.GetSaslMechanisms` returns (ADR-0112 decision 2, ADR-0107 decision 2).
- The `--auth` row's help categories name `amqp1` but not `amqp`. Check which other servers read `--auth` before wording the sentence.

## Acceptance criteria

- [ ] The `--auth` option page, manual paragraph and AI help paragraph name the AMQP servers.
- [ ] `surl --help amqp` lists `--auth`.
- [ ] A named test in `Surl.Cli.UnitTests` pins the new sentence.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
