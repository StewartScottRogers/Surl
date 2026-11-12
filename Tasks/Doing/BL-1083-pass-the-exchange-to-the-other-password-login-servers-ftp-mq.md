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
completed:
---
# BL-1083 — Pass the exchange to the other password-login servers (FTP, MQTT, LDAP, Redis, others)

## Goal

The FTP, MQTT, LDAP and Redis servers pass `LoginExchange.Of(context)` on their `PasswordLogin` and `SaslExchangeStart`, so their login events carry the exchange.

## Context

BL-871 (ADR-0128 Amendment 1) added the optional `Exchange` parameter. Until each server passes it, its logins are published with no exchange ID. Any other server calling `CheckPasswordLoginAsync` or `StartSaslExchange` is covered here too; add its project to `touches` first.

## Acceptance criteria

- [ ] One named test per server shows the policy receiving the login request with an `Exchange` whose ID and scheme are the connection's.
- [ ] The whole solution builds and the fast tests are green.

## Notes

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
