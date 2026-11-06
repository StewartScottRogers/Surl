---
id: BL-797
title: Answer a chosen protocol error by fault in the MQTT and LDAP servers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-823]
touches: [Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests, Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary, Protocols/Messaging/Surl.Protocol.Ldap.UnitTests, Documentation/Planning/Decisions/ADR-0139-how-the-mqtt-and-ldap-servers-answer-a-chosen-error.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
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

- [x] Named tests: MQTT `SUBSCRIBE` to `a/b` under a rule gets `SUBACK` `0x80` while `c/d` is granted; MQTT
      `CONNECT` under a rule gets the rule's `CONNACK` code; LDAP search under base `dc=x` with a rule gets the
      rule's `resultCode`; each publishes one `FaultAppliedEvent`.
- [x] A named test shows an MQTT rule scoped to account `alice` fires only for `alice`.
- [x] With no rule, every existing test of both libraries passes unchanged.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

- Decided in ADR-0139 (Claude under Stewart's delegation). ADR-0136 and ADR-0138 were already taken on other
  lanes' branches, so the next number free everywhere was 0139.
- `touches` grew by the ADR file and `Documentation/Planning/Decisions/README.md`: the decision needed
  recording and no task in Doing names either.
- MQTT 3.1.1 has no reason codes, so the rule's MQTT 5 reason code (128 to 255, the `Surl.Cli` shape) maps
  to the 3.1.1 `CONNACK` code of the same meaning (`0x84`→1, `0x85`→2, `0x86`→4, `0x87`→5, else 3, server
  unavailable); a `SUBSCRIBE` filter gets `0x80` whatever the code; a `PUBLISH` (any QoS) closes the
  connection with nothing published, as ADR-0124 says for 3.1.1.
- LDAP asks at `search` (base DN), `compare` (entry DN) and the four writes (no path, as they are not
  decoded); bind, StartTLS, other extended operations, abandon and unbind ask nothing. `LdapBindAnswer`
  gained `Account` so account-scoped rules work for simple and SASL binds.
- Each library has its own `FaultListenUrl` copy, as FTP and TFTP do; BL-908 folds them into `ListenUrl`.
- Named tests: `MqttFaultTests.ServeAsync_SubscribeToABAndCDUnderARuleOnAB_AnswersSubAck0x80ForABAndGrantsCD`,
  `...ServeAsync_ConnectUnderARule_AnswersTheRulesConnackCodeAndCloses`, `...ServeAsync_ARuleScopedToAlice_FiresForAliceOnly`,
  `LdapFaultTests.ServeAsync_SearchUnderBaseDcXWithARule_AnswersTheRulesResultCodeAndNoEntry`.
- Upstream curl 8.21.0 exit codes, from measurements of the same bytes: `SUBACK` `0x80` → 8 (ADR-0102 R12);
  `CONNACK` non-zero → 8 (BL-115, codes 4 and 5); a QoS 0 publish → 0 (R14, curl waits on nothing); an LDAP
  search with a non-success resultCode → 39 (as `noSuchObject`, measured). No new run: a live `surl` applies
  the plan only once BL-803 lands; BL-912 is filed to measure them then.
- Coverage measured with the MSTest collector (cobertura): both libraries 100% line and 100% branch.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-06: Doing -> Done. A chosen-error fault rule now fails an MQTT CONNECT, SUBSCRIBE filter or PUBLISH and an LDAP search, compare or write on purpose, publishing a FaultAppliedEvent (ADR-0139)
