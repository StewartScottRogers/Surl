---
id: BL-797
title: Answer a chosen protocol error by fault in the MQTT and LDAP servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests, Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary, Protocols/Messaging/Surl.Protocol.Ldap.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-797 — Answer a chosen protocol error by fault in the MQTT and LDAP servers

## Goal

When a chosen-error fault rule matches, the MQTT 3.1.1 server answers `CONNECT` with the rule's `CONNACK` return
code or a `SUBSCRIBE` to a topic with `SUBACK` failure `0x80`, and the LDAP server answers the matching operation
with the rule's `resultCode` and diagnostic message, so a test can make one subscribe, publish or search fail on
purpose.

## Context

- Decision: BL-822's ADR decision 4 (what "command" and "path" mean here - MQTT packet type and topic, LDAP
  operation and base DN; what a fault on an MQTT `PUBLISH`, which has no error answer at QoS 0, does).
- Contract: BL-823 (`ExchangeContext.Faults`, `FaultAppliedEvent`). Tests use BL-823's scripted fault plan and
  recording publisher with `InMemoryConnection`.
- Where: `Surl.Protocol.Mqtt` and `Surl.Protocol.Ldap`. Consult the plan per packet or operation, after any login,
  before the message bus or directory.
- Request bytes from pinned upstream curl 8.21.0 (`Record-CurlExchange.ps1`), never the Curl port (ADR-0003).
  Record in Notes the exit code upstream curl 8.21.0 gives for each named case.
- Stand-in: no store change; visible as a `FaultAppliedEvent`.

## Acceptance criteria

- [ ] Named tests: MQTT `SUBSCRIBE` to `a/b` under a rule gets `SUBACK` `0x80` while `c/d` is granted; MQTT
      `CONNECT` under a rule gets the rule's `CONNACK` code; LDAP search under base `dc=x` with a rule gets the
      rule's `resultCode`; each publishes one `FaultAppliedEvent`.
- [ ] A named test shows an MQTT rule scoped to account `alice` fires only for `alice`.
- [ ] With no rule, every existing test of both libraries passes unchanged.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-05: Created.
