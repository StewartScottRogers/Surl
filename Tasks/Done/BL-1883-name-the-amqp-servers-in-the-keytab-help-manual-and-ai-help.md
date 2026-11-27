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
completed: 2026-11-27
---
# BL-1883 — Name the AMQP servers in the --keytab help, manual and AI help text

## Goal

The --keytab manual section and AI help say that the AMQP 0-9-1 and AMQP 1.0 servers SASL `GSSAPI` logins use the keytab, for the service `amqp`.

## Context

- Found by BL-1858: ADR-0057 amendment 4 makes `amqp`, `amqps`, `amqp1` and `amqp1s` answer the Kerberos service `amqp`.
- `ManualText.cs` (the `--keytab` paragraph) and `AiHelpProse.cs` name only the SMTP, IMAP, POP3 and LDAP servers SASL `GSSAPI` logins; check the `--auth` `gssapi` texts and `CommandLineOptions.cs` too.
- Until BL-1884 is Done, an AMQP 1.0 ticket over 512 bytes does not reach the check; say only what is true when this runs.

## Acceptance criteria

- [x] The manual and the AI help paragraph on `--keytab` name the AMQP servers and the service `amqp`.
- [x] A named test in `Surl.Cli.UnitTests` pins that sentence.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- `ManualText.cs` and `AiHelpProse.cs`: the service list now has `amqp` for `amqp`, `amqps`, `amqp1` and `amqp1s`, and the SASL `GSSAPI` sentence names the AMQP 0-9-1 and AMQP 1.0 servers.
- Both also state the frame limits, because BL-1884 is not Done: a ticket is checked only when its frame fits, 4096 bytes on AMQP 0-9-1 before `connection.tune-ok` (`Amqp0FrameLimits.FrameMinSize`) and 512 for an AMQP 1.0 SASL frame (`Amqp1SecurityNegotiation` reads with `MinMaxFrameSize`). BL-1887 brings this into line with BL-1884.
- `CommandLineOptions.cs` unchanged: the `--keytab` row names no server, and the `gssapi` sentences name only `--keytab`. The `--auth` texts' "For SMTP, IMAP and POP3 logins" leaves AMQP out of the mechanism list as a whole, not only `gssapi`; filed as BL-1886.
- `--keytab`'s help categories (`auth`, `ldap`) are unchanged.
- Tests: `ManualTextTests.Text_SaysTheAmqpServersUseTheKeytabForTheServiceAmqpWithinTheirFrameLimits`, `AiHelpTextTests.Answer_Auth_SaysTheAmqpServersUseTheKeytabForTheServiceAmqpWithinTheirFrameLimits`.
- 2026-11-27 verify: build 0 warnings, format check clean, fast tests 30227 passed, 3 skipped, 0 failed. No `Integration` run: text only.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The --keytab manual and AI help name the AMQP 0-9-1 and AMQP 1.0 servers, the service amqp and their frame limits
