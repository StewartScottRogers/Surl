---
id: BL-1891
title: Name the AMQP servers' SASL logins in the --auth help, manual and AI help text
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1891 — Name the AMQP servers' SASL logins in the --auth help, manual and AI help text

## Goal

The `--auth` option page, manual paragraph and AI help paragraph say that `--auth` also sets the SASL mechanisms of the AMQP 0-9-1 and AMQP 1.0 servers, where that is true of the code.

## Context

- Found by BL-1883, by reading the code.
- `CommandLineOptions.AuthExplanation`, the `--auth` paragraph in `ManualText.cs` and the one in `AiHelpProse.cs` name SASL mechanisms "for SMTP, IMAP and POP3 logins" only. Both AMQP servers offer what `ISaslAuthenticationPolicy.GetSaslMechanisms` returns (ADR-0112 decision 2, ADR-0107 decision 2).
- The `--auth` row's help categories name `amqp1` but not `amqp`. Check which other servers read `--auth` before wording the sentence.

## Acceptance criteria

- [x] The `--auth` option page, manual paragraph and AI help paragraph name the AMQP servers.
- [x] `surl --help amqp` lists `--auth`.
- [x] A named test in `Surl.Cli.UnitTests` pins the new sentence.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Servers whose SASL mechanisms `--auth` sets, read from the code: SMTP, IMAP, POP3, LDAP, AMQP 0-9-1, AMQP 1.0, Kafka (of PLAIN and SCRAM-SHA-256/512) and XMPP (of SCRAM-SHA-256, SCRAM-SHA-1 and PLAIN). The sentence names all eight, so it is not true of only some.
- MQTT 5.0 is left out: `Surl.Composition` builds `Mqtt5Broker` with no SASL policy, so `surl` serves no enhanced authentication (BL-1899).
- Naming Kafka and XMPP made "Without it they are ..." incomplete, so the manual and AI help paragraphs now add that those two accept SCRAM too (ADR-0179). The option page's explanation has no such sentence.
- Only `amqp` was added to the `--auth` help categories. `kafka` and `xmpp` list none of the account options, a wider gap filed as BL-1898.
- Tests: `AiHelpTextTests.Answer_Auth_SaysAuthSetsTheSaslMechanismsOfEveryServerThatReadsItForSaslTheAmqpOnesIncluded` (AI help and manual), `HelpTextTests.Answer_Auth_IsItsPageWithItsDefaultWrappedAndItsExplanation` (option page), `HelpTextTests.Answer_AmqpCategory_ListsAuth`.
- Verify: build 0 warnings, format clean, fast tests 30275 passed, 3 skipped, 0 failed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The --auth help page, manual and AI help name the AMQP servers and every other SASL server that reads it, and --help amqp lists --auth
