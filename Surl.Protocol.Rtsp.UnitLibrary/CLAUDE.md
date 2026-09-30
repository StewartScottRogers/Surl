# Surl.Protocol.Rtsp.UnitLibrary

Phase 5.

The RTSP server (RFC 2326): the requests upstream curl's `--rtsp-request` sends -
`OPTIONS`, `DESCRIBE`, `ANNOUNCE`, `SETUP`, `PLAY`, `PAUSE`, `TEARDOWN`,
`GET_PARAMETER`, `SET_PARAMETER`, `RECORD` - and interleaved RTP.

**URL schemes answered:** `rtsp`

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
