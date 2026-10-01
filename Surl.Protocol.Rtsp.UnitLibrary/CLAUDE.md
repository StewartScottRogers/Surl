# Surl.Protocol.Rtsp.UnitLibrary

Phase 5.

The RTSP server (RFC 2326): the requests upstream libcurl's `CURLOPT_RTSP_REQUEST` sends -
`OPTIONS`, `DESCRIBE`, `ANNOUNCE`, `SETUP`, `PLAY`, `PAUSE`, `TEARDOWN`,
`GET_PARAMETER`, `SET_PARAMETER`, `RECORD` - and interleaved RTP. The `curl` tool has no
option that chooses the request: it sends only `OPTIONS *`. How every request is answered
is decided in ADR-0074.

**URL schemes answered:** `rtsp`

What exists (BL-313, BL-314 and BL-315, ADR-0074 decisions 1 to 5, 7 and 8's head limits
and session rows):

- `RtspProtocolServer` - the `IConnectionProtocolServer` and `IConnectionRefusalWriter` (`503`
  with no `CSeq`). Takes the `IAuthenticationPolicy` and starts one `IHttpAuthenticationSession`
  per connection with its `TlsSession` (none for `rtsp://`). Reads RTSP/1.0 heads with
  `Surl.HttpMessage`'s `HttpConnectionReader` for `HttpMessageProtocol.Rtsp10`, one after
  another on the connection.
- `RtspRequestResponder` - judges each request in ADR-0074 decision 2's order, the login
  (decision 7: `401` with the session's `WWW-Authenticate` values, or `403`, both keeping the
  connection) included, and answers `OPTIONS` (`Public`, all ten methods) and `DESCRIBE` (an
  SDP of the content-store file). `RtspRequestResponder.Sessions.cs` answers `SETUP`, `PLAY`,
  `PAUSE`, `TEARDOWN`, `GET_PARAMETER` and `SET_PARAMETER` (decision 5) and streams a playing
  session's frames between requests (`StreamUntilARequestArrivesAsync`, called by the server
  before each head is read). `ANNOUNCE`, `RECORD` and `SETUP` with `mode=record` are `501 Not
  Implemented` until BL-316 builds them.
- `RtspSession` - the connection's one session: ID, presentation, transport, playing state,
  position, sequence numbers and timestamps, the 60-second timeout (checked when a request
  names it). `RtspTransport` - the `Transport` alternative taken, or why none is (`461`).
  `RtspInterleavedFrame` - the `$` frames: RTP packets and the closing RTCP sender report and
  `BYE`.
- `RtspSessionDescription` - decision 4's SDP; `RtspStatus` - the statuses and their reason
  phrases; `RtspUnreadRequestDrainer` - the lingering close after a closing refusal.

Choices BL-315 made inside decision 5 (recorded in the task; BL-335 folds them into ADR-0074):
a client that half-closes while a session plays is streamed the rest before the connection
closes; a `SETUP` naming the session for another presentation is `455`; `PLAY` while playing
carries on from the current position; refusals of a request naming the live session name it
back; the sender report's RTP timestamp is the last packet's, its counts the session's.

The random source is an injected `RandomNumberGenerator` (as the POP3 server's); the
two-argument constructor uses the system one.

`Surl.Protocol.Rtsp.UnitTests/Fixtures` holds the `OPTIONS` exchanges recorded from pinned
upstream curl, with the response bytes curl accepted; its README says how each was recorded.

This library references `Surl.Protocol.Abstractions.UnitLibrary`, and may also reference
the horizontal libraries in ADR-0002 decision 3's table, as later ADRs amend it - nothing
else. Referencing another protocol server is a
build break, and `Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`
here. The server receives its transport from the listener seam in
`Surl.Protocol.Abstractions`, so the tests in the matching `.UnitTests` project can drive
it with request bytes measured from pinned upstream curl, with no network.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003).
