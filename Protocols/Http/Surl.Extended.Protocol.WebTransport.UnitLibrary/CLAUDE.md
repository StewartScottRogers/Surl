# Surl.Extended.Protocol.WebTransport.UnitLibrary

An extended server (ADR-0083): it will answer the `webtransport` scheme the Curl port adds, which
upstream curl does not have. ADR-0117 decides how. It is judged by WebTransport over HTTP/3,
draft-ietf-webtrans-http3-16 (WTH3), RFC 9297, RFC 9220 and RFC 9221, never by the Curl port's
output, and its unit tests replay bytes written by hand from those specifications' layouts.

It references `Surl.Protocol.Abstractions` and `Surl.Quic` (for `QuicVariableLengthInteger`, RFC 9000
section 16's codec, reused rather than copied) so far; BL-459 adds `Surl.Http3` when it serves
sessions.

What is here so far (BL-458, ADR-0117 decisions 4 and 6) - the codecs, with no transport:

- Capsules (RFC 9297 section 3.2): `WebTransportCapsuleType` names the codepoints;
  `WebTransportCloseSessionCapsule` (32-bit code, UTF-8 message of at most 1024 bytes, refused past
  that), `WebTransportDrainSessionCapsule`, `WebTransportDatagramCapsule`, `WebTransportLimitCapsule`
  (the six flow-control capsules carrying one variable-length integer) and `WebTransportUnparsedCapsule`
  (unknown types, and `WT_MAX_STREAM_DATA` and `WT_STREAM_DATA_BLOCKED`, which HTTP/3 prohibits).
  `WebTransportCapsuleWriter` writes one; `WebTransportCapsuleReader.Read` reads one and says
  `Incomplete` while it has not all arrived and `Malformed` when its value breaks its layout, and
  `TryReadHeader` gives the declared length before the value arrives.
- Stream headers (WTH3 4.2, 4.3): `WebTransportStreamHeader` writes and reads `0x41` or `0x54` and the
  session ID.
- `WebTransportApplicationErrorCode` maps 32-bit application codes into `WT_APPLICATION_ERROR`'s
  HTTP/3 range and back (WTH3 4.4).

Still to come: sessions over `Surl.Http3`, the echo, closing and draining (BL-459); flow control, the
session limit, authentication and the limits (BL-460).
