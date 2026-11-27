---
id: BL-1858
title: Decide which Kerberos service an AMQP SASL GSSAPI login answers, or stop the AMQP servers offering GSSAPI
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1858 — Decide which Kerberos service an AMQP SASL GSSAPI login answers, or stop the AMQP servers offering GSSAPI

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

- [x] An ADR-0057 amendment states the Kerberos service each AMQP scheme answers, or that the AMQP servers offer no `GSSAPI`.
- [x] A named test shows the offer or the service principal as the amendment states, for `amqp` and `amqp1`.
- [x] The comment on `GssapiSaslExchange.ServiceOf` names every server that starts a SASL exchange.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Decision (ADR-0057 amendment 4, Claude under delegation): AMQP keeps `GSSAPI`; `amqp`, `amqps`, `amqp1` and
  `amqp1s` answer the service `amqp`. Evidence: Apache Qpid Proton C and Qpid JMS use `amqp`; the AMQP 1.0
  security part names no service and IANA's registry has no `amqp` entry. No Curl port evidence.
- Tests: `GssapiSaslMechanismTests.Login_MutualAuthentication_IsAcceptedAsTheTicketsPrincipal` (rows `amqp`,
  `AMQPS`, `amqp1`, `amqp1s`) and `Login_AmqpSchemeWithAPopTicket_IsRefusedAfterTheDelay` (`amqp`, `amqp1`);
  six cases failed before the change.
- SASL starters found: SMTP, IMAP, POP3, LDAP, AMQP 0-9-1, AMQP 1.0, Kafka, XMPP, MQTT 5. The last three
  never offer `GSSAPI`.
- Pipeline: planned, tested, implemented and reviewed in this run without subagent stages (one method changed).
  No upstream curl conformance step: upstream curl has no AMQP, and no AMQP projects changed.
- Limit, filed as BL-1878: AMQP 1.0 closes a SASL frame over 512 bytes, so a large ticket never reaches the check.
- Filed also BL-1877 (`--keytab` help text names AMQP) and BL-1879 (a `Surl.Core.UnitTests` delay test failed
  once under load, passed on rerun and in the final full run).
- Verify: build 0 warnings, format clean, fast tests 30218 passed, 0 failed, 3 skipped.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. AMQP SASL GSSAPI logins check tickets for amqp/<host> on amqp, amqps, amqp1 and amqp1s (ADR-0057 amendment 4)
