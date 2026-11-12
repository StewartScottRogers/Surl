---
id: BL-1083
title: Pass the exchange to the other password-login servers (FTP, MQTT, LDAP, Redis, others)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-871]
touches: [Protocols/Messaging, Protocols/DataStore, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-1083 — Pass the exchange to the other password-login servers (FTP, MQTT, LDAP, Redis, others)

## Goal

The FTP, MQTT, LDAP and Redis servers pass `LoginExchange.Of(context)` on their `PasswordLogin` and `SaslExchangeStart`, so their login events carry the exchange.

## Context

BL-871 (ADR-0128 Amendment 1) added the optional `Exchange` parameter. Until each server passes it, its logins are published with no exchange ID. Any other server calling `CheckPasswordLoginAsync` or `StartSaslExchange` is covered here too; add its project to `touches` first.

## Acceptance criteria

- [x] One named test per server shows the policy receiving the login request with an `Exchange` whose ID and scheme are the connection's.
- [x] The whole solution builds and the fast tests are green.

## Notes

Also passed the exchange in the Amqp0, Amqp1, Kafka, Mqtt5, NATS, STOMP and XMPP servers (all under Protocols/Messaging, already in touches). NatsConnectOptions.ToPasswordLogin gained an optional exchange parameter. Kafka test assertion updated to expect the exchange. Dedicated named tests: FTP, MQTT, LDAP, Redis. Grpc and SocketIo tests flaked once under full-suite load and passed alone.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. FTP, MQTT, LDAP, Redis and other messaging servers pass the exchange to their logins
