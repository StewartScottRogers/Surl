---
id: BL-1788
title: Log received QUIC datagrams and HTTP/3 DATA payloads as received in the exchange log unless they can carry a secret
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1775]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Core.UnitLibrary, Foundation/Surl.Core.UnitTests, Protocols/Http/Surl.Http3.UnitLibrary, Protocols/Http/Surl.Http3.UnitTests, Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1788 — Log received QUIC datagrams and HTTP/3 DATA payloads as received in the exchange log unless they can carry a secret

## Goal

The HTTP/3 and WebTransport exchange logs show received QUIC datagrams and `DATA` payloads as received, unless they can carry a secret.

## Context

- Accepted limit of ADR-0182 amendment 35 (BL-1775), found by reading the code while planning it.
- `Surl.Core`'s `EventPublishingDecryptedBytesSink` gives QUIC datagrams (`Decrypted`, no stream ID) and
  `ReceivedFramePayload` chunks to `Http3RequestBytesRedactor.Redact`, which reads them as HTTP/3 frames, so bytes that
  read as a `HEADERS` frame (a WebTransport datagram or capsule, a request body) are withheld.
- It likely needs `Surl.Protocol.Abstractions` and `Surl.Core` to tell the redactor which kind of chunk it has. Other
  users of chunks with no stream ID, such as DTLS, keep their redaction.

## Acceptance criteria

- [x] A fast test shows a WebTransport datagram shaped like a `HEADERS` frame noted as received.
- [x] A fast test shows a `DATA` payload of the same shape noted as received.
- [x] A fast test shows a secret `HEADERS` frame on a request stream still withheld.
- [x] An ADR-0182 amendment states the rule, and `dotnet build -warnaserror` and the fast tests are green.

## Notes

- Rule (ADR-0182 amendment 37): `Surl.Protocol.Abstractions` gains `IStreamlessAndFramePayloadReceivedBytesRedactor`.
  `EventPublishingDecryptedBytesSink` calls it for received `Decrypted` bytes with no stream ID and for frame payloads;
  `Http3RequestBytesRedactor` implements it and writes both as received. Redactors without it (DTLS) behave as before.
- Rejected: a new `IDecryptedBytesSink` member for datagrams (the missing stream ID already says it, `Surl.Quic`
  unchanged); logging every datagram and payload as received in the engine (would drop DTLS redaction).
- Tests: `ServingEngineTests.DecryptedStreamRedaction.cs` (the datagram test uses the plain `Http3RequestBytesRedactor`;
  WebTransport's differs only in stream markers and secret names, and a session-marker redactor is tested directly in
  `Http3RequestBytesRedactorTests`).
- The `feature` plan and tests-first stages were done in this session (new tests seen failing first); code review by the
  `code-reviewer` agent found no secret newly logged and one uncovered arm (`Wire` on the sink), now tested. No
  conformance run: no byte on the wire changes, and upstream curl does not read surl's log.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. HTTP/3 and WebTransport exchange logs note received QUIC datagrams and DATA payloads as received; secret HEADERS frames on streams are still withheld (ADR-0182 amendment 37)
