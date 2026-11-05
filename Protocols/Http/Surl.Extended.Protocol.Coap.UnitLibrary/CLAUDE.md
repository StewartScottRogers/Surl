# Surl.Extended.Protocol.Coap.UnitLibrary

An extended server (ADR-0083): it answers the `coap` scheme the Curl port adds, which upstream
curl does not have. ADR-0097 decides how. It is judged by its specifications - RFC 7252, RFC 7959,
RFC 7641 and RFC 6690 - never by the Curl port's output, and its unit tests replay bytes written
by hand from those RFCs.

What is here so far is the message codec and the server's message layer (BL-471, ADR-0097
decisions 0 and 2):

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
- `CoapProtocolServer` (scheme `coap`, `IDatagramProtocolServer`) serves every message of a flow
  until the engine cancels it: a CON format error, Empty CON, response or reserved code gets an
  Empty RST of its Message ID; the same NON, every ACK and RST, and every non-CoAP datagram are
  ignored and noted. Until BL-472 serves resources, a well-formed request is answered `4.04`
  with its token: piggybacked in an ACK for a CON, in a NON with the flow's next Message ID
  (random first, then +1) for a NON.

Resources, methods and options (BL-472), block-wise transfers (BL-473), observation (BL-474),
`coaps` (BL-475) and the command line (BL-476) build on it.

This library references `Surl.Protocol.Abstractions.UnitLibrary` and `Surl.Content.UnitLibrary`,
and may also reference the horizontal libraries ADR-0084 and ADR-0097 allow (`Surl.Dtls` from
BL-475) - never another protocol server, original or extended. Never construct a `Socket`,
`TcpListener`, `UdpClient`, `SslStream` or `HttpListener` here; the transport comes from the
listener seam.
