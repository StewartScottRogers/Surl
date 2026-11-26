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
completed:
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

- [ ] A fast test shows a WebTransport datagram shaped like a `HEADERS` frame noted as received.
- [ ] A fast test shows a `DATA` payload of the same shape noted as received.
- [ ] A fast test shows a secret `HEADERS` frame on a request stream still withheld.
- [ ] An ADR-0182 amendment states the rule, and `dotnet build -warnaserror` and the fast tests are green.

## Notes

## Log

- 2026-11-26: Created.
