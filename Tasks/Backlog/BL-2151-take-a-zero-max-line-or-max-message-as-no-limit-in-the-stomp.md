---
id: BL-2151
title: Take a zero --max-line or --max-message as no limit in the STOMP broker
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitTests]
requirement: NFR-014
created: 2026-11-28
completed:
---
# BL-2151 — Take a zero --max-line or --max-message as no limit in the STOMP broker

## Goal

With `--max-line 0` the `stomp` and `stomps` broker holds no command or header line limit, and with `--max-message 0` its frame body bound is the next bound that applies, never 0 (ADR-0006 section 1). This task changes nothing in `Documentation/Product/Requirements.md`; BL-2157 re-states NFR-014 to NFR-016.

## Context

Found while working BL-2120 (2026-11-28). `Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitLibrary/StompFrameReader.cs` is built in `StompServerConnection.cs` with `context.Limits.MaxLineBytes` and `context.Limits.MaxMessageBytes` as given:

- `ThrowIfLineOverLimit` throws when `lineBytes > maxLineBytes`, so 0 refuses every line.
- `ThrowIfBodyOverLimit` throws when `bodyBytes > maxBodyBytes`, so 0 refuses every non-empty body, with or without `content-length`.

ADR-0118 decision 8 gives the body bound as the smaller of `--max-message` and the bus's `MaxMessageBytes`; with `--max-message` 0 that is the bus's bound (`long.MaxValue` under `--max-filesize 0`). With no limit, `ReadBodyAsync` casts `content-length` to `int`: a body that one array cannot hold (with its NUL) must still be refused `Frame too large`, and the buffer doubling must not overflow. The 128-header bound stays. Apply the same guard to any other use of these limits in the project (`StompServerConnection.Routing.cs` already guards its `MaxUploadBytes`). Stand-in: a test that turns a limit off sends a large frame through the shared message bus; nothing new to seed, reset or publish on the event stream.

## Acceptance criteria

- [ ] A test in `StompFrameReaderTests` reads a header line longer than 8192 bytes with a line limit of 0.
- [ ] Tests with `MaxMessageBytes` 0 read a body longer than 1048576 bytes, once with `content-length` and once ended by NUL.
- [ ] A test with `MaxMessageBytes` 0 shows a `content-length` above `int.MaxValue` is answered `ERROR` `Frame too large`, with no exception of another type.
- [ ] A test with `MaxMessageBytes` 0 and a small bus bound still refuses a body past the bus bound.
- [ ] The existing tests for non-zero limits still pass, `dotnet build Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitTests -warnaserror` is clean, and the fast tests pass with no `TestCategory=Integration` needed.

## Notes

## Log

- 2026-11-28: Created.
