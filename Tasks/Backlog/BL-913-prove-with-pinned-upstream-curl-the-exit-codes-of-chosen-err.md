---
id: BL-913
title: Prove with pinned upstream curl the exit codes of chosen-error faults on MQTT and LDAP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-797, BL-803, BL-1035]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Planning/Decisions/ADR-0139-how-the-mqtt-and-ldap-servers-answer-a-chosen-error.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-913 — Prove with pinned upstream curl the exit codes of chosen-error faults on MQTT and LDAP

## Goal

Integration tests run the pinned upstream curl 8.21.0 against a live `surl` given `--fault error` rules for
`mqtt` and `ldap`, and pin the exit code curl gives for each.

## Context

- BL-797 (ADR-0139) built the answers and tested them at connection level; a live `surl` applies them only
  once BL-803 composes the fault plan into the running server.
- ADR-0139, "What upstream curl does with these answers", lists what the same bytes already measured: `SUBACK`
  `0x80` exit 8 (R12), `CONNACK` 4 and 5 exit 8 (BL-115), a QoS 0 publish exit 0 (R14), an LDAP search
  `noSuchObject` exit 39. Never seen: `CONNACK` 3 (the default for an unmapped reason code), an LDAP search
  answered `busy` (51), and `sizeLimitExceeded` (4), which curl's source reads as success.
- Use the existing `UpstreamCurlLogsInToSurlOverMqttTests`, `UpstreamCurlTalksToSurlOverMqttTests` and
  `UpstreamCurlSearchesSurlOverLdapTests` / `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests` harnesses,
  `[TestCategory("Integration")]`, the pinned build only (Inconclusive without it), never the Curl port (ADR-0003).

## Acceptance criteria

- [ ] Named integration tests: an MQTT subscribe to `a/b` under `error;scheme=mqtt;command=SUBSCRIBE;path=a/b;reply=135`,
      an MQTT connect under a `CONNECT` rule with reply `136` (CONNACK 3), an MQTT `-d` publish under a
      `PUBLISH` rule, an LDAP search under `error;scheme=ldap;command=search;reply=51 busy` and one with
      `reply=4 limit`, each asserting the exit code as measured with the curl version stated.
- [ ] ADR-0139's "What upstream curl does with these answers" states the measured codes.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- 2026-11-11 (lane 4): `dotnet --list-sdks` shows no 10.0.4xx SDK (only 10.0.100-rc.1, 11.0.x), so `global.json` (10.0.401, rollForward latestFeature) cannot resolve and `dotnet build` fails before any integration test can run. Added BL-1035 to depends-on and returned the task to Backlog, as BL-895 and BL-910 were.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run
