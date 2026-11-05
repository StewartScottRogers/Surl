# Surl.Extended.Protocol.Amqp1.UnitLibrary

An extended server (ADR-0083): the AMQP 1.0 container that answers the `amqp1` and
`amqp1s` schemes the Curl port adds, which upstream curl does not have. ADR-0107 decides how. It is
judged by its specification, OASIS AMQP 1.0 (2012-10-29), never by the Curl port's output, and its
unit tests replay bytes written by hand from that specification.

The type system and the frame codec (BL-510; ADR-0107 Part 1 and decision
7's frame limits):

- `Amqp1Decoder` reads every value of Part 1 sections 1.2 to 1.6 in every width: the zero-width
  forms (`0x40` to `0x45`), the one-octet `boolean` (`0x56`), the small and wide integers, floats,
  decimals (kept as bits), `char` (a `Rune`), `timestamp`, `uuid`, `binary`, `string`, `symbol`,
  `list`, `map` (`Amqp1Map`, pairs in order), `array` (`Amqp1Array`, keeping its element format
  code and any element descriptor) and described values (`Amqp1Described`). Each width of a type
  reads as one .NET type. Anything malformed throws `Amqp1DecodeException` with
  `amqp:decode-error`: an unknown format code, a value past the end, elements that do not fill
  exactly their list, map or array, an odd map count, a boolean octet other than 0 or 1, a
  surrogate `char`, malformed UTF-8, a non-ASCII symbol, nesting deeper than 64, or an array of a
  zero-width element claiming more than 65536 elements.
- `Amqp1Encoder` writes each value in its smallest encoding (`Amqp1Encoder.FormatCodeFor`), and an
  `Amqp1Array` with its own element format code, so an array read is written back byte for byte;
  arrays are the only way `0x56` is written.
- `Amqp1Frame` and `Amqp1FrameReader` are Part 2 section 2.3's frames. The reader judges the 8-byte
  header first - size below 8, size above the `max-frame-size` given, DOFF below 2 or past the
  frame, a type other than AMQP `0x00` or SASL `0x01` - and refuses each as
  `amqp:connection:framing-error` without reading the rest; it skips the extended header when DOFF
  is above 2. An empty frame (no body) is a heartbeat. `Amqp1FrameLimits` holds MIN-MAX-FRAME-SIZE
  (512) and the `max-frame-size` offered for a `--max-message` (at most 65536).
- `Amqp1FrameBody.Read` reads the performative at the start of a frame body and the payload after
  it. The nine AMQP performatives (`Amqp1Open` to `Amqp1Close`) and the five SASL ones
  (`Amqp1SaslMechanisms` to `Amqp1SaslOutcome`) are records whose fields default as section 2.7
  says when left out; `Amqp1Error` is `error`. A body that is not a described list naming a
  performative of its frame's type is `amqp:connection:framing-error`; a mandatory field missing
  or any field of the wrong type is `amqp:decode-error`. A `symbol` field with `multiple="true"`
  reads a single symbol or an array, and writes one symbol as itself and more as an array.
  `source`, `target` and delivery states are kept as the described values read; `Amqp1Terminus` reads source,
  target and coordinator (BL-512), and BL-513 types delivery states.
- `Amqp1MessageSection.ReadAll` splits a message into its Part 3 section 3.2 sections, each with its
  bytes exactly as they arrived; `Amqp1Header` and `Amqp1Properties` read the two sections that are
  described lists.

Protocol headers and SASL (BL-511; ADR-0107 decisions 1 and 2):

- `Amqp1ProtocolHeader` writes and identifies the 8-byte headers `AMQP 0|2|3 1 0 0`
  (`Amqp1ProtocolId`); any other 8 bytes identify as none.
- `Amqp1SecurityNegotiation` runs one connection's headers and SASL until AMQP frames follow
  (`Amqp1SecurityNegotiationResult`: ready, and the account logged in). A SASL header runs SASL; a
  plain AMQP header is let through only under `AllowsAnonymous`; a TLS header on plaintext with
  `CanUpgradeToTls` is answered and upgraded, then negotiated again; anything else is answered
  `AMQP 3 1 0 0` and closed. `sasl-mechanisms` lists the policy's mechanisms then `ANONYMOUS` when
  allowed (an empty array when none); `sasl-init` with a mechanism not offered gets outcome 1, and
  every other login goes through `ISaslAuthenticationPolicy`, each challenge a `sasl-challenge`, each
  answer a `sasl-response`. SCRAM's server-final travels as the outcome's `additional-data`. After
  outcome 0 only `AMQP 0 1 0 0` is accepted; anything else, a further SASL frame included, is
  answered `AMQP 0 1 0 0` and closed. A SASL frame over 512 bytes, an AMQP frame or a frame out of
  order closes with no bytes. Headers and frames are read exactly, so pipelined bytes are kept.

Connections, sessions and links (BL-512; ADR-0107 decisions 3, 4 and 7):

- `Amqp1ContainerConnection` serves one connection once its headers and SASL are done, given the
  account logged in. It answers the client's `open` with the container's (`Amqp1ContainerOptions`:
  `container-id` `surl-` and 32 hex digits, `max-frame-size` from `--max-message`, `channel-max` 63,
  `idle-time-out` half of `--idle-timeout`, `product` = `surl`). Before `open` a frame the reader
  refuses, or the client going, ends it with no bytes; any other frame, or a client `max-frame-size`
  below 512, is answered with `open` then `close` and the error. After `open`: `close` is answered
  with `close`; a second `open`, a bad frame, a SASL frame, a `begin` with `remote-channel`, above
  channel 63 or on a channel in use, or a frame on a channel with no session closes with
  `amqp:connection:framing-error`; a performative that does not decode with `amqp:decode-error`.
- Sessions (`Amqp1Session`) answer on the client's channel when free, else the lowest free one within
  both `channel-max`s. `end` is answered with `end`. A session ended with an error (`end` with
  `amqp:session:handle-in-use`, `amqp:session:unattached-handle`, or `amqp:resource-limit-exceeded`
  when no handle of the container's is free) discards its frames until the client's `end`, which is
  not answered.
- Links (`Amqp1Link`): `attach` gets the opposite role, the same name, the lowest free handle of the
  container's, and the node's terminus (`Amqp1Terminus`): `distribution-mode` `copy` for a topic and
  `move` for a queue, `initial-delivery-count` 0 when the container sends, `max-message-size`
  `--max-filesize` and `rcv-settle-mode` `first` when it receives; the client's other terminus is
  echoed and no `unsettled` map is sent. `Amqp1NodeAddress` maps the address (one leading `/`
  dropped, `topic/<t>`, `queue/<q>`, any other name a queue) and `IAmqp1NodeDirectory` finds or
  creates the node and judges access; a coordinator, an address naming no node, or the directory's
  refusal gets an `attach` with a null terminus then a closing `detach` with the error. `detach` is
  answered with the same `closed`, unless the container already detached the link. `flow` and
  `transfer` on an unattached handle end the session with `amqp:session:unattached-handle`; on an
  attached one they, and `disposition`, are left to BL-513.
- Idle time-outs run on `ExchangeContext.TimeProvider`: nothing read for `--idle-timeout` closes with
  `amqp:resource-limit-exceeded` (`idle time-out expired`); with a nonzero client `idle-time-out` an
  empty frame is written whenever nothing was written for half of it, at most every 100 ms.
- Every error `description` is fixed text, never an address, account or exception.

Transfers, credit, settlement and the bus-backed `IAmqp1NodeDirectory` are BL-513; one server running
the negotiation and the connection under the head timeout and `--max-time` is BL-755; the command
line and composition BL-514.

This library references `Surl.Protocol.Abstractions.UnitLibrary`, and may also reference the
horizontal libraries ADR-0107 decision 0 names (`Surl.MessageBus`) - never another protocol
server, original or extended. Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream`
or `HttpListener` here; the transport comes from the listener seam.
