---
id: BL-2128
title: Take a zero --max-line or --max-message as no limit in the NATS server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests]
requirement: NFR-014
created: 2026-11-28
completed:
---
# BL-2128 — Take a zero --max-line or --max-message as no limit in the NATS server

## Goal

With `--max-line 0` the `nats` and `natss` server holds no control-line limit, and with `--max-message 0` its payload bound is the next bound that applies, never 0 (ADR-0006 section 1). This task changes nothing in `Documentation/Product/Requirements.md`; BL-2135 re-states NFR-014 to NFR-016.

## Context

Found while working BL-2120 (2026-11-28), in `Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary`:

- `NatsControlLineReader.ThrowIfOverLimit` throws when `lineBytes > maxLineBytes`, so 0 refuses every line.
- `NatsServer.ServeAsync` sets `maxPayload` to `Math.Min(Math.Min(context.Limits.MaxMessageBytes, bus.MaxMessageBytes), Array.MaxLength - 2)`, so `--max-message 0` advertises `max_payload` 0 in `INFO` and refuses every non-empty publish.

ADR-0113 decision 7 gives the bound as the smaller of `--max-message` and the bus's `MaxMessageBytes`; with `--max-message` 0 that is the bus's bound (`Surl.Composition` maps `--max-filesize 0` to `long.MaxValue`), still capped at `Array.MaxLength - 2`. `INFO` advertises the bound in force. With no line limit the reader's buffer doubling must not overflow `int`. Apply the same guard to any other use of these limits in the project. Stand-in: a test that turns a limit off publishes a large message through the shared message bus; nothing new to seed, reset or publish on the event stream.

## Acceptance criteria

- [ ] A test in `NatsControlLineReaderTests` reads a control line longer than 8192 bytes with a line limit of 0.
- [ ] A server test with `MaxMessageBytes` 0 shows `INFO`'s `max_payload` is the smaller of the bus's `MaxMessageBytes` and `Array.MaxLength - 2`, and that a `PUB` longer than 1048576 bytes is accepted.
- [ ] A server test with `MaxMessageBytes` 0 and a small bus bound still answers `-ERR 'Maximum Payload Violation'` for a publish past the bus bound.
- [ ] The existing tests for non-zero limits still pass, `dotnet build Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests -warnaserror` is clean, and the fast tests pass with no `TestCategory=Integration` needed.

## Notes

## Log

- 2026-11-28: Created.
