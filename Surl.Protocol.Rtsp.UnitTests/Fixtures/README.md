# RTSP fixtures

Recorded on 2026-09-30 (BL-313) from upstream curl 8.21.0, the win-x64 reference build pinned in
`UpstreamCurlBuilds.json` (SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`), with
`Record-CurlExchange.ps1 -Raw`, from the repository root in PowerShell. Never from the Curl port
(ADR-0003).

Each recorded folder holds the recorder's files - `request.bin` (the bytes curl sent),
`stdout.bin`, `stderr.txt`, `exitcode.txt` and `transcript.txt` - plus `response.bin`: the exact
bytes the scripted reply sent, which is the answer `Surl.Protocol.Rtsp` gives that request at
2026-09-28 12:00:00 UTC (ADR-0074 decisions 1 to 3). Pinned upstream curl completed each exchange
as the table says before any test pinned the bytes. The files are embedded resources of
`Surl.Protocol.Rtsp.UnitTests`; `.gitattributes` keeps git from rewriting their CRLF line endings.

`<ok>` is
`'RTSP/1.0 200 OK\r\nCSeq: {CSEQ}\r\nDate: Mon, 28 Sep 2026 12:00:00 GMT\r\nServer: surl\r\nPublic: OPTIONS, DESCRIBE, ANNOUNCE, SETUP, PLAY, PAUSE, TEARDOWN, GET_PARAMETER, SET_PARAMETER, RECORD\r\n\r\n'`;
`<501>` and `<400>` are the same status line and the first three fields with no `Public`.

| Folder | Command line | Exit, stdout |
| --- | --- | --- |
| `options-plain` | `.\Record-CurlExchange.ps1 -Raw -Port 18554 -OutDirectory ...\options-plain -RawReply <ok> -CurlArgs '-sS','-i','rtsp://127.0.0.1:18554/media'` | 0, the response head |
| `options-fields` | `... -RawReply <ok> -CurlArgs '-sS','-v','-H','X-Test: 1','-A','surl-test/1','-e','http://ref/','rtsp://127.0.0.1:18554/media/a.sdp'` | 0; stderr shows `Connection #0 ... left intact` |
| `not-implemented-501` | `... -RawReply <501> -CurlArgs '-sS','-f','-w','%{response_code}','rtsp://127.0.0.1:18554/media'` | 22, `501` |
| `bad-request-400` | `... -RawReply <400> -CurlArgs '-sS','-w','%{response_code}','rtsp://127.0.0.1:18554/media'` | 0, `400` |

The login cases (BL-314, ADR-0074 decision 7) were recorded the same day with the same build.
`<401>` is the status line `RTSP/1.0 401 Unauthorized`, the first three fields, then
`WWW-Authenticate: Digest realm="surl", qop="auth", algorithm=MD5, nonce="MDAwMDAwMDAwMDAwMDAwMA"` and
the same with `algorithm=SHA-256`; `<403>` is `RTSP/1.0 403 Forbidden` with the first three fields.
None has a body, so curl keeps the connection.

| Folder | Command line | Exit, stdout |
| --- | --- | --- |
| `no-login-401` | `... -RawReply <401> -CurlArgs '-sS','-f','rtsp://127.0.0.1:18554/media'` | 22, `The requested URL returned error: 401` |
| `digest-login` | `... -RawReply <401>,<ok> -CurlArgs '-sS','-v','--digest','-u','tester:secret','rtsp://127.0.0.1:18554/media'` | 0; the first `OPTIONS *` carries no `Authorization`, the second, `CSeq: 2` on the same connection, `Authorization: Digest username="tester",...,uri="*",...,algorithm=MD5,...` |
| `basic-login` | `... -RawReply <ok> -CurlArgs '-sS','-w','%{response_code}','-u','tester:secret','rtsp://127.0.0.1:18554/media'` | 0, `200`; `Authorization: Basic dGVzdGVyOnNlY3JldA==` sent unasked |
| `basic-forbidden-403` | `... -RawReply <403> -CurlArgs '-sS','-w','%{response_code}','-u','tester:secret','rtsp://127.0.0.1:18554/media'` | 0, `403` |

`rfc2326-describe/request.bin` is not a recording: the `curl` tool sends only `OPTIONS *`, and
`DESCRIBE` is libcurl's alone (ADR-0074 decision 12). Until BL-333 records libcurl's, it is
RFC 2326 section 10.2's example request, with CRLF line endings:
`DESCRIBE rtsp://server.example.com/fizzle/foo RTSP/1.0`, `CSeq: 312`,
`Accept: application/sdp, application/rtsl, application/mheg`.
