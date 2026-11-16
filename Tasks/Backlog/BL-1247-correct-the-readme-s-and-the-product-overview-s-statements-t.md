---
id: BL-1247
title: Correct the README's and the product overview's statements that the MQTT 5, AMQP, Kafka and Redis servers are unregistered and that LDAP accounts are unbuilt
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [README.md, Documentation/Product/Product-Overview.md]
requirement: none
created: 2026-11-16
completed:
---
# BL-1247 — Correct the README's and the product overview's statements that the MQTT 5, AMQP, Kafka and Redis servers are unregistered and that LDAP accounts are unbuilt

## Goal

`README.md` and `Documentation/Product/Product-Overview.md` say what the code does now about the shared message bus and LDAP as an account source, so an agent reading them is not told that built and registered servers are missing.

## Context

Found by the second stand-in audit (2026-11-16). Root `CLAUDE.md`, "Say what it does, do what it says": a document that states something false of the code is a defect.

- `README.md`, the numbered list near the top, item 3 ("Shared stores"), ends: "The MQTT 5, AMQP, Kafka and Redis servers are written against the bus but `surl` does not register them yet." False: `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs` registers `amqp`/`amqps`, `amqp1`/`amqp1s`, `kafka`/`kafkas`, `mqtt5`/`mqtt5s`/`mqtt5+ws` and `redis`/`rediss` over the one `MessageBus`, and `Conformance/Surl.Conformance.UnitTests/CurlPortClientConsumesMessagesSharedAcrossSurlProtocolsTests.cs` (MQTT to AMQP 0-9-1, AMQP 1.0, STOMP, NATS, Kafka and MQTT 5) and `StandInPublishAndConsumeRecipeTests.cs` (MQTT to Redis) prove them on it.
- `README.md`, item 4 ("A configuration command line"), ends: "LDAP as a source of accounts is not built yet." False: `--user-ldap` is an option row in `Application/Surl.Cli.UnitLibrary/CommandLineOptions.cs` (ADR-0149, BL-673), composed by `LdapAccountSourceComposition`.
- `Documentation/Product/Product-Overview.md`, the bullet "One store per job, per `surl` run": "today `mqtt`, `mqtts`, `nats`, `natss`, `stomp` and `stomps` are registered on it ... The `mqtt5`, `amqp`, `amqp1`, `kafka` and `redis` libraries are written against the bus, and registering each in `surl` is intent: BL-975, BL-507, BL-514, BL-973 and BL-492." Those tasks are Done.
- Prose only; no code, no help text. Name the extended servers as extended (`SURL_EXTENSIONS=0` turns them off, ADR-0083), and keep every statement checkable against the files above.

## Acceptance criteria

- [ ] `README.md` item 3 names the servers on the shared bus as the code registers them (original: `mqtt`, `mqtts`; extended: MQTT 5, AMQP 0-9-1, AMQP 1.0, Kafka, NATS, STOMP, Redis) and no longer says any is unregistered.
- [ ] `README.md` item 4 says `--user-ldap` reads accounts from an LDAP directory, linking ADR-0149, and no longer says it is unbuilt.
- [ ] `Documentation/Product/Product-Overview.md`'s "One store per job, per `surl` run" bullet lists every scheme registered on the bus today and no longer calls their registration intent.
- [ ] `grep -n "not register them yet\|is not built yet\|registering each in .surl. is intent" README.md Documentation/Product/Product-Overview.md` prints nothing.

## Notes

## Log

- 2026-11-16: Created.
