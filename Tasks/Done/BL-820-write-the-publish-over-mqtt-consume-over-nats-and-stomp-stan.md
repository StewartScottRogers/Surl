---
id: BL-820
title: Write the publish-over-MQTT, consume-over-NATS-and-STOMP stand-in recipe against a Surl.Api server
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-640, BL-528, BL-533, BL-807]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-12
---
# BL-820 — Write the publish-over-MQTT, consume-over-NATS-and-STOMP stand-in recipe against a Surl.Api server

## Goal

A recipe test class shows a message published over MQTT by a real client being consumed over NATS and over STOMP
from one `Surl.Api` stand-in, through the shared message bus, with the test awaiting the bus's publish event
rather than polling.

## Context

- Same recipe rules as BL-817: short, commented, `[TestCategory("Integration")]` in
  `Conformance/Surl.Conformance.UnitTests`, no sleeps or polling.
- Built on: BL-640 (MQTT publish read on another listener), BL-528 (NATS served from the executable) and BL-533
  (STOMP served from the executable), so both are composed when extensions are on
  (`SurlServerOptions.AreExtensionsTurnedOn = true`), and BL-807 (waits).
- Publisher: pinned upstream curl 8.21.0, `curl -d <payload> mqtt://127.0.0.1:<port>/<topic>`. Consumers: upstream
  curl speaks neither NATS nor STOMP, and the Curl port is not a recipe's client, so each consumer is a few lines
  of a test-local `TcpClient` writing the text frames the specifications define - NATS `CONNECT {}` and
  `SUB <subject> 1` (NATS client protocol), STOMP 1.2 `CONNECT` and `SUBSCRIBE` frames - with the bytes taken from
  those specifications, the extended servers' oracle (ADR-0083 decision 1). Topic names map as ADR-0100 decision
  3.2 says (MQTT `a/b` is NATS `a.b`; STOMP's destination per `StompDestinationNames`).
- Stand-in: publish over one broker protocol, consume over others, through the message bus.

## Acceptance criteria

- [x] `StandInPublishAndConsumeRecipeTests` exists with a NATS consumer test and a STOMP consumer test, each
      receiving the exact payload curl published, each awaiting the bus's publish event, passing on Windows with the
      pinned build, platform-neutral.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Two Integration tests, both pass against pinned curl 8.21.0. Topic `a/b`: NATS subject `a.b`, STOMP `/topic/a/b`. Consumers subscribe first (NATS PING/PONG, STOMP RECEIPT prove it), then curl publishes and the test awaits the bus StoreChanged event. STOMP frames use LF as the server writes them (spec canonical form).

## Log

- 2026-11-05: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Recipe tests show curl MQTT publish consumed over NATS and STOMP from one Surl.Api server
