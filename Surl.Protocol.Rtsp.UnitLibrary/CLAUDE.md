# Surl.Protocol.Rtsp.UnitLibrary

Phase 5.

The RTSP server (RFC 2326): the requests upstream libcurl's `CURLOPT_RTSP_REQUEST` sends -
`OPTIONS`, `DESCRIBE`, `ANNOUNCE`, `SETUP`, `PLAY`, `PAUSE`, `TEARDOWN`,
`GET_PARAMETER`, `SET_PARAMETER`, `RECORD` - and interleaved RTP. The `curl` tool has no
option that chooses the request: it sends only `OPTIONS *`. How every request is answered
is decided in ADR-0074.

**URL schemes answered:** `rtsp`

What exists (BL-313, ADR-0074 decisions 1 to 4 and 8's head limits):

- `RtspProtocolServer` - the `IConnectionProtocolServer` and `IConnectionRefusalWriter` (`503`
  with no `CSeq`). Reads RTSP/1.0 heads with `Surl.HttpMessage`'s `HttpConnectionReader` for
  `HttpMessageProtocol.Rtsp10`, one after another on the connection.
- `RtspRequestResponder` - judges each request in ADR-0074 decision 2's order and answers
  `OPTIONS` (`Public`, all ten methods) and `DESCRIBE` (an SDP of the content-store file).
  The other eight methods are `501 Not Implemented` until BL-314 to BL-316 build them; the
  login (decision 7) and sessions (decision 5) are not checked yet.
- `RtspSessionDescription` - decision 4's SDP; `RtspStatus` - the statuses and their reason
  phrases; `RtspUnreadRequestDrainer` - the lingering close after a closing refusal.

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
