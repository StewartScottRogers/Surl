---
id: BL-2117
title: Store an MQTT 5 retained publish before the broker answers the next connection's SUBSCRIBE, as v0.1.12 did
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Mqtt.UnitLibrary, Protocols/Messaging/Surl.Protocol.Mqtt.UnitTests, Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-28
completed:
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

- [ ] A unit test in `Surl.Protocol.Mqtt.UnitTests` (or `Surl.MessageBus.UnitTests`, where the
      cause is) publishes a retained MQTT 5 message, disconnects, then subscribes on a new
      connection with no clock advance and no delay, and receives the message; it fails on the
      v0.1.13 code and passes after the fix.
- [ ] The same test for MQTT 3.1.1 still passes.
- [ ] The repro above, run with a pinned upstream curl build (`Record-CurlExchange.ps1` or a
      pinned path from `UpstreamCurlBuilds.json`), prints `71` and exits 0; the output is
      quoted in this task's Notes.
- [ ] The commit that caused it is named in Notes, with why the ordering changed.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
