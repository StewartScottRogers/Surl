# Surl.Quic.UnitLibrary

Phase 6.

QUIC version 1 for Surl's HTTP/3 server, hand-built on every platform (ADR-0082 decisions 1, 2
and 4). What it holds now is the codec, in the clear (BL-373), packet protection (BL-374) and the
server connection through its handshake (BL-377), streams and flow control (BL-378, ADR-0096); loss
recovery with BL-379, key updates with BL-606, the client's spare connection IDs with BL-607 and
the closing and draining periods with BL-608.

- `QuicServerConnection` - one connection, no I/O: `ReceiveDatagram` takes the client's datagram
  and returns a `QuicConnectionOutput` (datagrams to send, each at most 1200 bytes, and the
  `QuicStream`s the client's frames reached); `HandleTimeout` acts at `NextDeadline`
  (the idle timeout discards it silently, `QuicServerConnectionOptions.HandshakeTimeout` closes it
  `NO_ERROR`); `SendApplicationFrames` seals 1-RTT frames once the handshake is complete; `Close`
  sends an application `CONNECTION_CLOSE`. It starts on a version 1 Initial in a datagram of at least
  1200 bytes whose Destination Connection ID is at least 8 bytes, answers another version of that
  size with Version Negotiation, and drops everything else before it starts. The handshake is
  `Surl.Tls13`'s `TlsServerHandshake` in `CRYPTO` frames, reassembled by `QuicCryptoReassembly`
  (64 KiB ahead at most); the client's transport parameters are checked (RFC 9000 sections 7.3 and
  18.2, RFC 9368) and the server's are ADR-0082 decision 4's. Every ack-eliciting packet is
  acknowledged at once, ACK Delay 0 (`QuicReceivedPacketNumbers`, 32 ranges kept). Before a
  Handshake packet opens, it sends at most three times what it received; Initial keys go when that
  packet opens, Handshake keys when the handshake completes. 1-RTT packets wait for the handshake
  to complete. Errors close with `CONNECTION_CLOSE`, no reason phrase: in 1-RTT after the handshake,
  before it at Initial and Handshake (RFC 9000 section 10.2.3). Nothing is retransmitted yet (BL-379).
- `QuicStream` - one stream (ADR-0096): `Read` returns the client's bytes in order (put back by
  `QuicOrderedBytes`, which `CRYPTO` shares), `Write(data, fin)` queues bytes, `Reset` and
  `StopSending` send their frames. Nothing is sent until `QuicServerConnection.Transmit` or the next
  `ReceiveDatagram`. The internal `QuicStreams` opens the client's streams as their frames arrive
  (with every lower one, up to `MAX_STREAMS`), opens the server's unidirectional streams
  (`OpenUnidirectionalStream`, null and `STREAMS_BLOCKED` past the client's limit), holds both
  sides' credit, re-grants `MAX_STREAM_DATA` and `MAX_DATA` a window at a time once half is read and
  `MAX_STREAMS` as client streams close, answers `STOP_SENDING` with `RESET_STREAM`, and sends
  `STREAM_DATA_BLOCKED` and `DATA_BLOCKED` once per limit. Past a stream limit is
  `STREAM_LIMIT_ERROR`, past a credit `FLOW_CONTROL_ERROR`, a broken final size `FINAL_SIZE_ERROR`,
  and a frame for a part a stream lacks, or for a server stream not opened, `STREAM_STATE_ERROR`.
- `QuicTransportParameters` reads and writes RFC 9000 section 18's parameters and
  `version_information` (`QuicVersionInformation`); a malformed or repeated one is
  `TRANSPORT_PARAMETER_ERROR`.

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
- `QuicInitialSecrets` - RFC 9001 section 5.2: the client and server Initial secrets from the
  client's first Destination Connection ID, and the `QuicPacketProtection` pair they key.
- `QuicPacketProtection` - RFC 9001 sections 5.1 to 5.5, one direction at one encryption level:
  `FromTrafficSecret` derives `quic key`, `quic iv` and `quic hp`; `Protect` seals a
  `QuicLongHeaderPacket` or `QuicShortHeaderPacket` and applies header protection; `Unprotect`
  removes it, recovers the full packet number (`QuicPacketNumber.Decode`) and opens the payload.
  A packet whose tag fails, or that is too short to sample, comes back as
  `QuicUnprotectStatus.Undecryptable` (drop it), never thrown; reserved bits set after opening
  come back as `ReservedBitsNotZero` (a `PROTOCOL_VIOLATION`). AES-GCM and AES-ECB are the BCL's;
  ChaCha20-Poly1305 and the ChaCha20 mask are the hand-built `Surl.Cryptography.ChaCha20` and
  `Surl.Cryptography.Poly1305` (ADR-0082 decision 3), behind the internal `QuicPacketCipher`.
- `QuicRetryIntegrity` - RFC 9001 section 5.8: computes and checks a Retry's integrity tag.
- `QuicReader`, `QuicWriter`, `QuicFrameTypes`, `QuicPacketHeaderBits`, `QuicConnectionIds`,
  `QuicPacketNumberSpace`, `QuicReceivedPacketNumbers`, `QuicCryptoReassembly`, `QuicOrderedBytes`, `QuicStreams` and the
  `QuicPacketCipher` classes are internal, visible to `Surl.Quic.UnitTests` only.

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
decision 2), and references all three. It does no I/O: never open a socket or a file, and
never read a clock - time arrives through an injected `TimeProvider` once there is a connection.

Every expected value in `Surl.Quic.UnitTests` comes from RFC 9000 (Appendix A.1's varints) and
RFC 9001 Appendix A's keys, unprotected and protected packets (A.1 to A.5), never from the Curl port (ADR-0003).
The connection tests replay the Initial datagrams pinned upstream curl builds sent
(`Fixtures/ClientInitial`, recorded by `Record-CurlExchange.ps1 -QuicInitial`; BL-377's Notes say
how) through `UnitTestQuicClient`, which puts its own x25519 share in the ClientHello and completes
the handshake with its own TLS 1.3 client, `UnitTestTls13Client`.
