---
id: BL-508
title: Run the Curl port's amqp client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-507, BL-406]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/Amqp0.md]
requirement: none
created: 2026-11-04
completed: 2026-11-12
---
# BL-508 — Run the Curl port's amqp client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl amqp://` and `amqps://`: publish a
message, get it back, get from an empty queue, and a refused bad login. Each assertion comes from AMQP 0-9-1, and
the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help amqp` and its cases in `Curl.Conformance.Extended.UnitTests/Amqp0/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [x] The test class exists with the four exchanges, each passing on Windows with the pinned client present.
- [x] `Documentation/Conformance/Extended/Amqp0.md`: every row is covered by a named test or marked `n/a` with a reason.
- [x] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

- Four exchanges (publish then get, get from empty, get after taken, bad login 403) in `CurlPortClientExchangesMessagesWithSurlOverAmqp0Tests`; publish-then-get runs for amqp and amqps.
- Port finding (not a surl defect, never evidence): the port client exits 0 for `-T file` to `amqp://` yet nothing reaches the queue on surl; `-d` publishes. Tests use `-d`.
- The 11 `BL-504` placeholder rows in Amqp0.md (wire framing) now name their `Surl.Extended.Protocol.Amqp0.UnitTests` tests, so every row is covered or n/a.

## Log

- 2026-11-04: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. The Curl port's amqp client publishes, gets, gets-empty and is refused a bad login against a live surl; Amqp0 matrix fully covered
