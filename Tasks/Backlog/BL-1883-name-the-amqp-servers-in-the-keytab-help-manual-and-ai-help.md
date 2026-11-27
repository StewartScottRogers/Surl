---
id: BL-1883
title: Name the AMQP servers in the --keytab help, manual and AI help text
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1883 — Name the AMQP servers in the --keytab help, manual and AI help text

## Goal

The --keytab manual section and AI help say that the AMQP 0-9-1 and AMQP 1.0 servers SASL `GSSAPI` logins use the keytab, for the service `amqp`.

## Context

- Found by BL-1858: ADR-0057 amendment 4 makes `amqp`, `amqps`, `amqp1` and `amqp1s` answer the Kerberos service `amqp`.
- `ManualText.cs` (the `--keytab` paragraph) and `AiHelpProse.cs` name only the SMTP, IMAP, POP3 and LDAP servers SASL `GSSAPI` logins; check the `--auth` `gssapi` texts and `CommandLineOptions.cs` too.
- Until BL-1884 is Done, an AMQP 1.0 ticket over 512 bytes does not reach the check; say only what is true when this runs.

## Acceptance criteria

- [ ] The manual and the AI help paragraph on `--keytab` name the AMQP servers and the service `amqp`.
- [ ] A named test in `Surl.Cli.UnitTests` pins that sentence.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
