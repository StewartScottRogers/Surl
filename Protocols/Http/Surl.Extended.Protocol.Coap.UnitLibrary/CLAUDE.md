# Surl.Extended.Protocol.Coap.UnitLibrary

An extended server (ADR-0083): it answers the `coap` scheme the Curl port adds, which upstream
curl does not have. ADR-0097 decides how. It is judged by its specifications - RFC 7252, RFC 7959,
RFC 7641 and RFC 6690 - never by the Curl port's output, and its unit tests replay bytes written
by hand from those RFCs.

What is here so far is the message codec (BL-471), the message layer and the resources (BL-472),
and block-wise transfers (BL-473), ADR-0097 decisions 0, 1, 2, 3, 4 and 6:

- `CoapMessage` is one message: `CoapMessageType`, `CoapCode` (`c.dd`), Message ID, a token of
  at most 8 bytes, `CoapOption`s kept in order of their numbers, and the payload. An Empty
  message (0.00) carries nothing else. `CoapOption.FromUInt` writes a uint in the fewest bytes;
  `ReadUInt` reads one with leading zero bytes.
- `CoapMessageWriter.Write` writes version 1, the options delta-encoded with the 13 and 14
  extensions, and the 0xFF marker only before a payload.
- `CoapMessageReader.Read` reads a datagram as a `CoapReadOutcome`: the message; `NotCoap` for a
  datagram shorter than the header or of another version; or `FormatError`, with the header's
  type and Message ID, for a token length of 9 to 15, a delta or length nibble of 15, a payload
  marker with no payload, a token or option past the datagram, an option number past 65535, or
  an Empty message with a token or bytes after its Message ID.
- `CoapProtocolServer` (scheme `coap`, `IDatagramProtocolServer`) takes the `ContentStore` and
  `CoapAccessRules` (`--allow-anonymous`, whether an account is configured) and serves each flow
  through a `CoapFlowSession` until the engine cancels it.
- `CoapFlowSession` is one flow's message layer: a CON format error, Empty CON, response or
  reserved code gets an Empty RST of its Message ID, and the same NON is ignored; a CON request is
  answered piggybacked when its answer is ready within 1 second, else with an Empty ACK and a
  separate CON response retransmitted from a random 2-3 s timeout, doubled, until a matching ACK or
  Empty RST, or given up after MAX_RETRANSMIT (4); one server CON outstanding at a time (NSTART
  1); a NON request gets a NON. `CoapDuplicateCache` (128 IDs) gives a duplicate CON the same bytes
  within EXCHANGE_LIFETIME and ignores a duplicate NON within NON_LIFETIME.
  `CoapTransmissionParameters` holds RFC 7252 section 4.8's defaults as constants.
- `CoapRequestOptions` reads a request's options: unknown, method-inapplicable, out-of-range and
  repeated non-repeatable options are unrecognised; an unrecognised critical one (odd, or a
  reserved Location-* number) is `4.02` with `Unrecognized option <n>` for a CON and ignores a NON.
- `CoapResourceAnswerer` answers, first match wins: `4.02`, `4.00` (Uri-Path `.` or `..`), `5.05`
  (any Proxy-Uri or Proxy-Scheme; BL-750 serves one naming the listener), `4.05` (any method but
  the four), `4.01` (`CoapAccessRules`), `4.05` (a write without `--allow-uploads`), then the
  resource: `GET` `2.05` with ETag, Max-Age 60 and Content-Format (`CoapContentFormat`, by
  extension) or `2.03` for a current ETag, `4.04`, `4.12`, `4.06`; `PUT` `2.01`/`2.04`; `POST`
  appends (`2.01`/`2.04`) or, to a directory, creates a file named by 32 random hex digits (`2.01`
  with Location-Path); `DELETE` `2.02`, `4.05` for a directory; `4.13` with Size1 past
  `--max-filesize`; `5.00` for an `IOException` or `UnauthorizedAccessException` from the store.
  `CoapEntityTag` is the first 8 bytes of SHA-256 over a file's length and UTC ticks.
- `CoapResourceDirectory` writes `/.well-known/core` under `--list-directories`:
  `</path>;ct=..;sz=..;obs` per visible file, ordinal order, at most 1000 links, 16 directories
  deep, filtered by the first `name=value` or `name=prefix*` query.
- `ICoapRandomSource` (`CryptographicCoapRandomSource` in production) draws Message IDs, the
  ACK_RANDOM_FACTOR fraction and POST names; tests inject a fixed one.

- Block-wise transfers (RFC 7959; ADR-0097 decision 4). `CoapBlockOption` is NUM/M/SZX in one
  uint option; SZX 7 in a request is `4.00`. `CoapBlock2Window` picks what a `2.05` carries: the
  whole representation when it fits one block (1024 bytes, SZX 6) and no Block2 was asked for,
  else the block named, in the smaller of the client's and the server's size, with Block2, and
  Size2 on block 0 or when the request sent Size2; a block past the end is `4.02`. Each block is
  read afresh as a byte range with the current ETag; `/.well-known/core` is blocked the same way.
  `CoapBlock1Uploads`, one per flow, keeps up to 4 atomic `ContentUploadSession`s keyed by
  Uri-Path and Uri-Query: block 0 opens one (replacing any), each block with M is `2.31` with
  Block1 in the size the server uses, the block without M commits and gets the method's code with
  Block1; out of order, no upload open, or a changed Content-Format is `4.08`; a repeat of the
  last block is `2.31` again, not written twice; Size1 or a total past `--max-filesize`, or a
  fifth upload, is `4.13` with Size1. An upload idle for EXCHANGE_LIFETIME, and every upload when
  the flow ends, is discarded with its temporary file. The preferred SZX is an internal
  constructor parameter (6 by default) so tests can replay RFC 7959's 128- and 32-byte figures
  and BL-475 can shrink it for DTLS.

Decision 7's other limits and the `5.03` refusal are BL-751. Observation (BL-474), `coaps` (BL-475) and the
command line (BL-476) build on it.

This library references `Surl.Protocol.Abstractions.UnitLibrary` and `Surl.Content.UnitLibrary`,
and may also reference the horizontal libraries ADR-0084 and ADR-0097 allow (`Surl.Dtls` from
BL-475) - never another protocol server, original or extended. Never construct a `Socket`,
`TcpListener`, `UdpClient`, `SslStream` or `HttpListener` here; the transport comes from the
listener seam.
