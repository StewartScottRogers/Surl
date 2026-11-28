---
id: BL-2153
title: Take --max-message 0 as no limit for coaps handshake messages
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests]
requirement: NFR-015
created: 2026-11-28
completed:
---
# BL-2153 — Take --max-message 0 as no limit for coaps handshake messages

## Goal

With `--max-message 0` a `coaps` DTLS handshake completes: `--max-message` bounds no handshake message (ADR-0006 section 1). This task changes nothing in `Documentation/Product/Requirements.md`; BL-2157 re-states NFR-014 to NFR-016.

## Context

Found while working BL-2120 (2026-11-28). `Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary/SecureCoapProtocolServer.cs` (`OptionsFor`) passes `(int)Math.Min(context.Limits.MaxMessageBytes, int.MaxValue)` as `DtlsServerOptions.MaximumMessageLength`. `Surl.Dtls`' `DtlsHandshakeReassembler` takes it as the most body bytes it may hold, so 0 makes every fragment of a non-empty message `OverLimit` (ADR-0087 decision 9's `handshake_failure`). Pass `int.MaxValue` for 0 here; `Surl.Dtls` needs no change, and each message stays bounded by its 24-bit length field. `CoapFlowSession` already treats 0 as no limit, and `CoapRefusals` leaves out `Block1` when no message limit refuses; check the project's other uses of the limit and guard any with the same fault. Stand-in: a test that turns the limit off can handshake with a long client certificate chain; nothing new to seed, reset or publish on the event stream.

## Acceptance criteria

- [ ] A test in `SecureCoapProtocolServerTests` completes a DTLS handshake with `MaxMessageBytes` 0 and gets an answer to a CoAP request over it.
- [ ] A test covers a `MaxMessageBytes` above `int.MaxValue` completing the handshake too.
- [ ] The existing test or a new one still shows a small non-zero `MaxMessageBytes` failing the handshake as ADR-0087 decision 9 says.
- [ ] `dotnet build Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests -warnaserror` is clean and the fast tests pass with no `TestCategory=Integration` needed.

## Notes

## Log

- 2026-11-28: Created.
