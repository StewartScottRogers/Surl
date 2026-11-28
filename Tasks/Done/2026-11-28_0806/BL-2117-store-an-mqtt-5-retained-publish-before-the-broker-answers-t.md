---
id: BL-2117
title: Store an MQTT 5 retained publish before the broker answers the next connection's SUBSCRIBE, as v0.1.12 did
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests, Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests, Documentation/Planning/Decisions/ADR-0093-how-the-mqtt-5-broker-answers-mqtt5-mqtt5s-and-mqtt5-ws.md, Documentation/Planning/Decisions/ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md, Documentation/Planning/Decisions/ADR-0014-how-the-mqtt-server-answers.md]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2117 — Store an MQTT 5 retained publish before the broker answers the next connection's SUBSCRIBE, as v0.1.12 did

## Goal

A publish over MQTT 5 on a topic that `--topic "<filter>;retain"` keeps is the retained message
before the broker answers a SUBSCRIBE on any later connection, so a test can publish and read
back in one `curl ... --next ...` command, as in v0.1.12.

## Context

A regression in v0.1.13 (tag commit `6117c6e4d`), reported by the Surly cookbook
(`Z:\repos\Surly\Documentation\Bug-Reports\2026-11-28T0730-Surl-bug-report.md`, item 1; read
only, never edit or commit in `Z:\repos\Surly`). The book stays pinned to v0.1.12 until it is
fixed. It breaks the recipes `mqtt5-publish-and-subscribe` (solution, clear-password-allowed,
over-tls), `mqtt5-refuse-a-publish-by-reason-code`, and probably
`mqtt-change-retained-config-mid-test`, on Windows, Linux and macOS.

Repro, with a pinned upstream curl build:
- `surl -v --allow-anonymous --mqtt5-disconnect-after-retained --topic "home/#;retain" mqtt5://127.0.0.1:18883/`
- `curl -s -m 5 -d 71 mqtt5://127.0.0.1:18883/home/kitchen/temp --next -m 5 mqtt5://127.0.0.1:18883/home/kitchen/temp`
  prints nothing, exit 0, on v0.1.13. On v0.1.12 it prints `71`.
- The `-v` log shows the publish received (`#1 < home/kitchen/temp ... 71`, then `\xE0\x00`,
  the client's DISCONNECT) and the subscribe answered with no retained message
  (`#2 * Sent the retained messages; sent DISCONNECT 0x00`).
- A second apart (two separate `curl` runs with `sleep 1` between) prints `71`. With an account
  (`--user alice:s3cret --allow-plaintext-auth`) it also prints nothing. `mqtt://` (3.1.1) with
  the same options works.

So the retained store is written after, or concurrently with, the next connection's
SUBSCRIBE answer: an ordering race on the MQTT 5 path only. A lead, not a conclusion: between
v0.1.12 and v0.1.13 the MQTT server library did not change, but `Surl.MessageBus.UnitLibrary`
did, notably `08a7aea77` (BL-2038, excluded publisher withheld at copy-in, touching
`BusConnection.cs`, `MessageBus.Queues.cs` and `MessageBus.SubscriptionQueues.cs`), `527ebc6d7`
and `8ed9e3937`. Bisect with `git log v0.1.12..v0.1.13 -- Protocols/Messaging` if the lead does
not hold. A test must drive time from the injected `TimeProvider`, not a sleep.

## Acceptance criteria

- [x] A unit test in `Surl.Protocol.Mqtt.UnitTests` (or `Surl.MessageBus.UnitTests`, where the
      cause is) publishes a retained MQTT 5 message, disconnects, then subscribes on a new
      connection with no clock advance and no delay, and receives the message; it fails on the
      v0.1.13 code and passes after the fix. (In `Surl.Extended.Protocol.Mqtt5.UnitTests`, the
      broker that answers `mqtt5`; see Notes.)
- [x] The same test for MQTT 3.1.1 still passes.
- [x] The repro above, run with a pinned upstream curl build (`Record-CurlExchange.ps1` or a
      pinned path from `UpstreamCurlBuilds.json`), prints `71` and exits 0; the output is
      quoted in this task's Notes. (Over `mqtt://`: no upstream curl build speaks `mqtt5`. The
      `mqtt5://` repro is shown with a client written from MQTT 5.0; see Notes.)
- [x] The commit that caused it is named in Notes, with why the ordering changed.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

**Cause (criterion 4).** No code change reordered anything: v0.1.12 loses the same race when it is
JIT-compiled. What changed is how `surl` is run. Commit `54f62a725` (BL-1984, released by `6233302de`)
packs `surl` as the framework-dependent .NET tool `Surly.Surl.Console` with `PublishAot` off, and Surly
moved from the native release binary to that tool for v0.1.13 (Surly ADR-0010 amendment 2). Under the
JIT the first `PUBLISH` compiles its code while the publisher's next connection runs, so the later
`SUBSCRIBE` overtakes it. The ordering was never guaranteed: two connections are two tasks. The leads
in Context (`08a7aea77`, `527ebc6d7`, `8ed9e3937`) are not the cause.

**Fix (ADR-0100 decision 3.11, decided by Claude under Stewart's delegation; ADR-0093 decision 9 and
ADR-0014 Amendment 2 point to it).** The bus keeps the connections that may publish, oldest first
(`MessageBus.PublishingConnections`). A one-shot `SUBSCRIBE` (`mqtt`, `mqtts`, and `mqtt5*` under
`--mqtt5-disconnect-after-retained`) is answered once every earlier connection has handled what its
client sent, for at most 2 seconds on the `TimeProvider`.

**Choices and why.**
- `touches` gained the MQTT 5 library and its tests: the extended broker answers `mqtt5`, not
  `Surl.Protocol.Mqtt`. Also ADR-0093, ADR-0100 and ADR-0014. No task in `Doing` named any of them.
- The 3.1.1 server is fixed too, though the report says it works: pinned upstream curl lost the same
  race over `mqtt://` in 21 of 30 runs, and criterion 3 can only be met over `mqtt://`.
- The tracker is in the bus, not in each broker, so a publish over one MQTT version is seen by a
  one-shot read over the other.

**Tests.** `ServeAsync_DisconnectAfterRetainedWhileAnEarlierPublisherIsStillBeingHandled_...`
(`Mqtt5BrokerTests`) failed on the unfixed broker (its library is unchanged since v0.1.13): the
subscriber got `SUBACK` then `DISCONNECT`, no retained message. It passes now. The 3.1.1 test is
`ServeAsync_SubscribeWhileAnEarlierPublisherIsStillBeingHandled_...` (`MqttRetainFlagTests`). The three
changed libraries measure 100% line and branch coverage, no failing member. MQTT live `Integration`
tests in `Surl.Conformance.UnitTests` passed (80).

**Pinned upstream curl (criterion 3).** `mqtt5://` is refused by the pinned build:
`curl: (1) Protocol "mqtt5" not supported`. So the report's `curl -d 71` is the Curl port, which is
never proof (ADR-0003). Over `mqtt://`, Git for Windows curl 8.21.0 (SHA-256 `0E773709...8778`),
against `surl -v --allow-anonymous --topic "home/#;retain" mqtt://127.0.0.1:18883/`, JIT build:
`curl -s -m 5 -d 71 mqtt://127.0.0.1:18883/home/kitchen/temp --next -s -m 5 -o repro.bin mqtt://127.0.0.1:18883/home/kitchen/temp`

    exit 0
    stdout bytes: 0011686F6D652F6B69746368656E2F74656D703731
    (topic length 00 11, "home/kitchen/temp", payload "71")

**Live race counts**, fresh `surl` each run, runs that read nothing:

| Build and client | Before | After |
| --- | --- | --- |
| Pinned upstream curl, `mqtt`, JIT | 21 of 30 | 0 of 60 |
| Spec-written client, `mqtt`, JIT | 60 of 100 | 0 of 100 |
| Spec-written client, `mqtt5`, JIT | 15 of 20 (v0.1.12: 13 of 20) | 0 of 100 |
| Spec-written client, `mqtt5`, native AOT | v0.1.12: 2 of 100 | 0 of 200 |

**Follow-up:** BL-2132 (publishers over other protocols do not join the wait).

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. A one-shot MQTT or MQTT 5 SUBSCRIBE is answered after earlier connections have handled what they received, so publish-then-read in one curl command works on JIT and native surl
