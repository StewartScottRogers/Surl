---
id: BL-1849
title: Decide which Kerberos service an AMQP SASL GSSAPI login answers, or stop the AMQP servers offering GSSAPI
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-27
completed:
---
# BL-1849 — Decide which Kerberos service an AMQP SASL GSSAPI login answers, or stop the AMQP servers offering GSSAPI

## Goal

An `amqp`, `amqps`, `amqp1` or `amqp1s` listener started with `--auth gssapi --keytab` either checks a ticket for a
service principal an ADR names, or does not offer `GSSAPI`.

## Context

- Found by the code review of BL-1819, by reading the code; not run.
- Both AMQP servers offer every mechanism the policy returns (`Amqp0BrokerConnection.Login.cs`, `OfferedMechanisms`;
  `Amqp1SecurityNegotiation.cs`), and their policy (`AuthenticationPolicyFor("amqp")` and `("amqp1")`) holds the
  Kerberos acceptor.
- `GssapiSaslExchange.ServiceOf` (`Foundation/Surl.Authentication.UnitLibrary`) answers `pop` for every scheme that
  is not SMTP, IMAP or LDAP, so an AMQP ticket has to name `pop/<host>`. Its comment says only the mail and LDAP
  servers start a SASL exchange. ADR-0057's decision 2 table has no AMQP row.
- The Kafka, XMPP and MQTT 5 servers leave `GSSAPI` out of their offers; check any other extended server with SASL.
- The `--keytab` texts in `Application/Surl.Cli.UnitLibrary` do not name AMQP. If AMQP keeps `GSSAPI`, file the help
  text change as its own task. If a fix needs the AMQP projects, add them to `touches`.
- AMQP is an extended server (ADR-0083): the specification and the Curl port's client are what apply, never as
  ADR evidence from the port.

## Acceptance criteria

- [ ] An ADR-0057 amendment states the Kerberos service each AMQP scheme answers, or that the AMQP servers offer no `GSSAPI`.
- [ ] A named test shows the offer or the service principal as the amendment states, for `amqp` and `amqp1`.
- [ ] The comment on `GssapiSaslExchange.ServiceOf` names every server that starts a SASL exchange.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
