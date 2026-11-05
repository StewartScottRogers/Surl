# Surl.Quic.UnitLibrary

Phase 6.

QUIC version 1 for Surl's HTTP/3 server and DNS over QUIC, hand-built on every platform (ADR-0082 decisions 1, 2
and 4). What it holds now is the codec, in the clear (BL-373), packet protection (BL-374) and the
server connection through its handshake (BL-377), streams and flow control (BL-378, ADR-0096), and
loss recovery and congestion control (BL-379), the flow loop an application protocol rides (BL-572), `DATAGRAM` frames (BL-573), `RESET_STREAM_AT` (BL-748), and following the client's key updates (BL-606); the client's spare connection IDs come with BL-607 and
the closing and draining periods with BL-608.

- `QuicServerConnection` - one connection, no I/O: `ReceiveDatagram` takes the client's datagram
  and returns a `QuicConnectionOutput` (datagrams to send, each at most 1200 bytes, and the
  `QuicStream`s the client's frames reached); `HandleTimeout` acts at `NextDeadline`
  (the idle timeout discards it silently, `QuicServerConnectionOptions.HandshakeTimeout` closes it
  `NO_ERROR`, the loss detection timer declares packets lost or sends a probe);
  `SendApplicationFrames` seals 1-RTT frames once the handshake is complete; `Close`
  sends an application `CONNECTION_CLOSE`. It starts on a version 1 Initial in a datagram of at least
  1200 bytes whose Destination Connection ID is at least 8 bytes, answers another version of that
  size with Version Negotiation, and drops everything else before it starts. The handshake is
  `Surl.Tls13`'s `TlsServerHandshake` in `CRYPTO` frames, reassembled by `QuicCryptoReassembly`
  (64 KiB ahead at most); the client's transport parameters are checked (RFC 9000 sections 7.3 and
  18.2, RFC 9368) and the server's are its options' (ADR-0082 decision 4's by default). Every ack-eliciting packet is
  acknowledged at once, ACK Delay 0 (`QuicReceivedPacketNumbers`, 32 ranges kept). Before a
  Handshake packet opens, it sends at most three times what it received; Initial keys go when that
  packet opens, Handshake keys when the handshake completes. 1-RTT packets wait for the handshake
  to complete. Errors close with `CONNECTION_CLOSE`, no reason phrase: in 1-RTT after the handshake,
  before it at Initial and Handshake (RFC 9000 section 10.2.3).
- Loss recovery (BL-379, RFC 9002): every packet sent is a `QuicSentPacket` in its space until
  acknowledged or lost. `QuicLossDetection` takes RTT samples into `QuicRttEstimator` (section 5;
  the client's ACK Delay scaled by its `ack_delay_exponent` and capped by its `max_ack_delay`, 25 ms
  by default), declares a packet lost by the packet threshold (3) or the time threshold (9/8 RTT),
  and arms the probe timeout (none at the anti-amplification limit or with nothing ack-eliciting in
  flight; Application Data only once the handshake is confirmed). Lost frames are sent again ahead
  of new ones as RFC 9000 section 13.3 says: `CRYPTO`, `HANDSHAKE_DONE`, `STREAM` unless the stream
  was reset, the current `MAX_DATA`, `MAX_STREAMS` and `MAX_STREAM_DATA` (while the stream still
  receives), everything else as it was; never `ACK`, `PADDING`, `PING`, `PATH_RESPONSE`, `DATAGRAM` or
  `CONNECTION_CLOSE`. A probe sends the space's waiting data, else its oldest packet's frames again,
  else a `PING`, whatever the congestion window. A `CRYPTO` or `STREAM` frame too long for the room
  left is split when the first part keeps at least 64 bytes. `QuicCongestionController` is NewReno
  (section 7, ADR-0082 decision 4): 12000-byte initial window, 2400-byte minimum, halved once per
  recovery period, the minimum on persistent congestion (judged on one space's losses); ACK-only
  packets are outside the window, and an ack-eliciting Initial waits for a datagram the window and
  the amplification limit let reach 1200 bytes. Spaces whose keys go are forgotten (section 6.4).
- Key updates (BL-606, RFC 9001 section 6, ADR-0082 decision 4): the server follows the client's and never starts one. The internal `QuicKeyPhase` holds the key phase bit every 1-RTT packet sent carries, the client's next keys made in advance (the next secret is HKDF-Expand-Label(secret, `quic ku`, "", hash length), `QuicPacketProtection.NextTrafficSecret`; `ForNextKeyPhase` keeps the header-protection key), and its previous keys, kept until the next update to open late packets of the old phase (numbered below the packet that started the current one). A packet in the other phase numbered from that packet on opens with the next keys and, once it opens, switches both directions to them. A key update before the server has sent an `ACK` in the current phase - the first update too, since a client may only update once a packet of its phase is acknowledged (section 6.1) - ends the connection `KEY_UPDATE_ERROR` (section 6.2). A packet in the other phase that does not open is dropped and the keys stay.
- `DATAGRAM` frames (BL-573, RFC 9221, ADR-0086 decision 4): off unless
  `QuicServerConnectionOptions.MaxDatagramFrameSize` is set; then `max_datagram_frame_size` (0x20) is
  sent, and each `QuicDatagramFrame` (type `0x30` to the end of the packet, `0x31` with a Length) the
  client sends comes back in `QuicConnectionOutput.ReceivedDatagramFrames` from `ReceiveDatagram`. One
  received when the option is null or 0, or larger than it (type and length counted), ends the
  connection `PROTOCOL_VIOLATION` with the frame's type. `SendDatagram` sends its data in a type `0x31`
  frame once the handshake is complete and the client sent a non-zero `max_datagram_frame_size`
  (`InvalidOperationException` otherwise; `ArgumentOutOfRangeException` for a frame over that or over
  `MaxApplicationFrameLength`). It is ack-eliciting and in flight, waits for the congestion window like
  other frames, and is never sent again when lost. The HTTP server never sets the option.
  `QuicApplicationConnection` carries them (BL-753): `SendDatagramAsync` is `SendDatagram` under its
  lock, and `ReceiveDatagramAsync` awaits a copy of each received frame's data, queued in order by
  the flow loop, at most `ReceivedDatagramCapacity` (128) waiting, the oldest dropped past it (RFC
  9221 section 5 lets a receiver drop any).
- `RESET_STREAM_AT` (BL-748, ADR-0117 decision 0, draft-ietf-quic-reliable-stream-reset-09): off
  unless `QuicServerConnectionOptions.ResetStreamAt` is set; then the empty `reset_stream_at` (0x1d)
  is sent (`QuicTransportParameters.ResetStreamAt`; a non-empty one read is
  `TRANSPORT_PARAMETER_ERROR`). `QuicResetStreamAtFrame` is type `0x24` (Stream ID, error code, Final
  Size, Reliable Size), 0-RTT and 1-RTT only; one read whose Reliable Size is above its Final Size is
  `FRAME_ENCODING_ERROR`. `QuicStream.ResetAt(code, reliableSize)` (and
  `QuicApplicationConnection.ResetAtAsync`) sends it when the option is set and the client sent the
  parameter, otherwise it is `Reset`, a plain `RESET_STREAM`: unsent bytes from the Reliable Size on
  are dropped with any `FIN`, the frame goes once every byte below it is sent, Final Size what was
  sent by then, and lost `STREAM` data is sent again only below the Reliable Size (cut, without
  `FIN`); a later reset of that sending part, or `STOP_SENDING`, sends nothing more. Received while
  the option is off it is a frame type the server does not know, `FRAME_ENCODING_ERROR`; otherwise
  bytes below the smallest Reliable Size received stay readable and the reset takes effect
  (`ResetErrorCode`) once `Read` has returned them all. A `RESET_STREAM` counts as Reliable Size 0, a
  larger Reliable Size later is ignored, and another error code is `STREAM_STATE_ERROR`. The HTTP
  server and DNS over QUIC never set the option.
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
- `QuicDatagramFlowServer` (BL-572, ADR-0086 decision 4) - the only part that touches I/O: its
  `ServeAsync` takes one `IDatagramFlow` (from `Surl.Protocol.Abstractions`), feeds the flow's
  datagrams to a `QuicServerConnection`, sends what it returns, waits for `NextDeadline` with
  `Task.Delay` on the injected `TimeProvider` and then calls `HandleTimeout`, and once the handshake
  is complete hands the connection to an `IQuicApplicationProtocol` (HTTP/3, DNS over QUIC). The
  application sees a `QuicApplicationConnection`, not the bare `QuicServerConnection`: the loop and
  the application work at once, so every call goes through one lock. It accepts the client's
  streams (`AcceptStreamAsync`), reads waiting for bytes (`ReadAsync`), receives `DATAGRAM` data
  (`ReceiveDatagramAsync`), and writes, resets, stops sending, sends `DATAGRAM` frames, opens
  unidirectional streams and closes, each sent at once. The application returning
  closes the connection with application error code 0; the connection ending (either side's
  close, idle, the server's token) cancels `Ended`, the application's token, after which reads
  return what is left then 0, datagram receives what is left then null, accepts null, and other
  calls throw `OperationCanceledException`.
  An exception the application throws closes the connection with code 0 and is thrown from
  `ServeAsync`. The ALPN IDs agreed and the transport parameters sent are the caller's
  `QuicServerConnectionOptions`; the defaults stay ADR-0082 decision 4's `h3` values, byte for byte.
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
  `QuicPacketNumberSpace`, `QuicReceivedPacketNumbers`, `QuicCryptoReassembly`, `QuicOrderedBytes`, `QuicStreams`,
  `QuicSentPacket`, `QuicLossDetection`, `QuicRttEstimator`, `QuicCongestionController`, `QuicKeyPhase` and the
  `QuicPacketCipher` classes are internal, visible to `Surl.Quic.UnitTests` only.

Malformed input throws `QuicTransportException`; byte strings read are slices of the input, not
copies. Writing a value RFC 9000 forbids throws an `ArgumentException`.

| Input | Error |
| --- | --- |
| a frame that ends early (a truncated varint too), a frame type neither RFC 9000, RFC 9221 nor draft-ietf-quic-reliable-stream-reset-09 defines, a `RESET_STREAM_AT` whose Reliable Size is above its Final Size, a `STREAM` frame ending beyond 2^62-1, an `ACK` range below packet number 0, a stream count above 2^60, an empty `NEW_TOKEN`, a `NEW_CONNECTION_ID` of 0 or over 20 bytes or retiring beyond itself | `FRAME_ENCODING_ERROR` |
| an empty payload, a frame type not in its shortest encoding, a frame the packet type does not permit (section 12.4, table 3) | `PROTOCOL_VIOLATION` |
| a `CRYPTO` frame ending beyond 2^62-1 | `CRYPTO_BUFFER_EXCEEDED` |
| any packet-header fault: early end, a version 1 connection ID over 20 bytes, fixed bit 0, reserved bits set, a Length shorter than the packet number, a Retry without its tag, a Version Negotiation list not whole versions | `PROTOCOL_VIOLATION` with no frame type: the caller drops the packet |

Namespace `Surl.Quic`. It may reference `Surl.Protocol.Abstractions.UnitLibrary` (ADR-0086 decision 5), `Surl.Tls13.UnitLibrary`,
`Surl.Cryptography.ChaCha20.UnitLibrary` and `Surl.Cryptography.Poly1305.UnitLibrary` (ADR-0082
decision 2), and references all four. Only `QuicDatagramFlowServer` does I/O, and only through the
`IDatagramFlow` it is given: never open a socket or a file, and
never read a clock - time arrives through an injected `TimeProvider` once there is a connection.

Every expected value in `Surl.Quic.UnitTests` comes from RFC 9000 (Appendix A.1's varints) and
RFC 9001 Appendix A's keys, unprotected and protected packets (A.1 to A.5), never from the Curl port (ADR-0003).
The connection tests replay the Initial datagrams pinned upstream curl builds sent
(`Fixtures/ClientInitial`, recorded by `Record-CurlExchange.ps1 -QuicInitial`; BL-377's Notes say
how) through `UnitTestQuicClient`, which puts its own x25519 share in the ClientHello and completes
the handshake with its own TLS 1.3 client, `UnitTestTls13Client`.
