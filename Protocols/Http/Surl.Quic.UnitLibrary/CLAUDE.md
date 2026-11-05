# Surl.Quic.UnitLibrary

Phase 6.

QUIC version 1 for Surl's HTTP/3 server, hand-built on every platform (ADR-0082 decisions 1, 2
and 4). What it holds now (BL-373) is the codec, in the clear; packet protection is BL-374, and
the connection, streams and loss recovery come with BL-377 to BL-379.

- `QuicVariableLengthInteger` - RFC 9000 section 16: writes the shortest encoding, reads any.
- `QuicPacketReader.Read` reads one packet from a datagram and says how many bytes it took, so
  coalesced packets are read one after another. It returns a `QuicLongHeaderPacket` (Initial,
  0-RTT, Handshake), `QuicRetryPacket`, `QuicVersionNegotiationPacket`,
  `QuicShortHeaderPacket` (1-RTT, told its connection ID length) or
  `QuicUnsupportedVersionPacket` (any other version: only its version and connection IDs, so the
  server can answer with Version Negotiation). Each but the last writes itself with `ToBytes`.
  A packet's `Payload` is everything its header covers after the packet number: frames in the
  clear, or ciphertext and AEAD tag.
- `QuicFrameReader.Read` reads a payload into `QuicFrame`s for the packet type that carried it;
  `QuicFrameWriter.Write` and `QuicFrame.ToBytes` write them. There is one record per frame of RFC
  9000 section 19; consecutive `PADDING` bytes read as one `QuicPaddingFrame`, `ACK` ranges are
  packet numbers (`QuicAckRange`), not wire gaps, and `STREAM`'s OFF/LEN/FIN bits come from its
  `Offset`, `HasLength` and `Fin`.
- `QuicTransportErrorCode` - RFC 9000 section 20.1. `QuicTransportException` carries the code
  and the frame type a `CONNECTION_CLOSE` reports.
- `QuicReader`, `QuicWriter`, `QuicFrameTypes`, `QuicPacketHeaderBits` and `QuicConnectionIds`
  are internal, visible to `Surl.Quic.UnitTests` only.

Malformed input throws `QuicTransportException`; byte strings read are slices of the input, not
copies. Writing a value RFC 9000 forbids throws an `ArgumentException`.

| Input | Error |
| --- | --- |
| a frame that ends early (a truncated varint too), a frame type RFC 9000 does not define, a `STREAM` frame ending beyond 2^62-1, an `ACK` range below packet number 0, a stream count above 2^60, an empty `NEW_TOKEN`, a `NEW_CONNECTION_ID` of 0 or over 20 bytes or retiring beyond itself | `FRAME_ENCODING_ERROR` |
| an empty payload, a frame type not in its shortest encoding, a frame the packet type does not permit (section 12.4, table 3) | `PROTOCOL_VIOLATION` |
| a `CRYPTO` frame ending beyond 2^62-1 | `CRYPTO_BUFFER_EXCEEDED` |
| any packet-header fault: early end, a version 1 connection ID over 20 bytes, fixed bit 0, reserved bits set, a Length shorter than the packet number, a Retry without its tag, a Version Negotiation list not whole versions | `PROTOCOL_VIOLATION` with no frame type: the caller drops the packet |

Namespace `Surl.Quic`. It may reference `Surl.Tls13.UnitLibrary`,
`Surl.Cryptography.ChaCha20.UnitLibrary` and `Surl.Cryptography.Poly1305.UnitLibrary` (ADR-0082
decision 2) and references none of them yet. It does no I/O: never open a socket or a file, and
never read a clock - time arrives through an injected `TimeProvider` once there is a connection.

Every expected value in `Surl.Quic.UnitTests` comes from RFC 9000 (Appendix A.1's varints) and
RFC 9001 Appendix A's unprotected packets (A.2 to A.5), never from the Curl port (ADR-0003).
