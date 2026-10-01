# ADR-0074 — How the RTSP server answers upstream curl

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-286 (FR-051).
- **Amends:** nothing. [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md)'s RTSP rows,
  [ADR-0015](ADR-0015-how-the-content-store-applies-the-exposure-options.md) (uploads),
  [ADR-0019](ADR-0019-how-the-http-server-enforces-the-hardening-limits.md) (`Server: surl`, the
  refusal deadline), [ADR-0026](ADR-0026-rtsp-conformance-on-macos-where-the-pinned-build-has-no-rtsp.md)
  (macOS is Inconclusive), [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md)
  sections 4 and 6, [ADR-0036](ADR-0036-digest-nonces-stale-replay-and-user-name-bytes.md),
  [ADR-0059](ADR-0059-how-a-protocol-server-tells-a-limit-from-shutdown.md) and
  [ADR-0070](ADR-0070-the-http-message-library-the-http-websocket-and-rtsp-servers-share.md) (heads
  are read and written with `Surl.HttpMessage`, protocol `RTSP/1.0`) are applied as written.
  [ADR-0071](ADR-0071-how-the-websocket-server-answers-upstream-curl.md) decision 10's
  `libcurl-4.dll` pin is adopted (decision 12). No contract in `Surl.Protocol.Abstractions` changes.

## Context

`surl rtsp://...` is to be the server upstream curl's `rtsp://` transfers talk to (FR-051). What
was left, and what this ADR decides from measurement, is every byte `Surl.Protocol.Rtsp` sends:
the response head and each refusal, what `DESCRIBE` describes and `PLAY` streams, sessions,
`ANNOUNCE` and `RECORD`, the logins, ADR-0006's limits in RTSP's words, the verbose notes and the
help category, so that BL-313 to BL-317 are built without a question and BL-318 knows what to
prove.

**What the `curl` tool can send, from upstream's own sources** (tag `curl-8_21_0`, read
2026-09-30): `lib/rtsp.c` sends only the request `CURLOPT_RTSP_REQUEST` names ("Since all RTSP
requests are included here, there is no need to support custom requests like HTTP"), and
`lib/url.c` defaults it to `RTSPREQ_OPTIONS`; the stream URI defaults to `*`
(`CURLOPT_RTSP_STREAM_URI`). No file in `docs/cmdline-opts` names RTSP, and `src/config2setopts.c`
only sets `User-Agent` and `Referer` for it. So the tool sends exactly one request, `OPTIONS *`;
`DESCRIBE`, `ANNOUNCE`, `SETUP`, `PLAY`, `PAUSE`, `TEARDOWN`, `GET_PARAMETER`, `SET_PARAMETER`,
`RECORD` and the interleaved receive (`RTSPREQ_RECEIVE`, `CURLOPT_INTERLEAVEFUNCTION`) are
libcurl's API only. `rtsp_do` sets `no_body` for every request but `DESCRIBE` and
`GET_PARAMETER`, so a body on any other response is not written. libcurl has no call that sends
RTP: a libcurl `RECORD` sends the request and nothing else.

### What upstream curl 8.21.0 does (measured)

Measured 2026-09-30 with the Windows reference build, `C:\Program Files\Git\mingw64\bin\curl.exe`,
SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`
(`curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel ...`), through
`Record-CurlExchange.ps1 -Raw -Port 18554`, extended by this task so a reply's `{CSEQ}` becomes the
value of the last `CSeq` field curl sent (the script's help, `-RawReply`). Each case gave one
scripted reply per pause in what curl sent; `U` is `rtsp://127.0.0.1:18554/media`.

**The request.** Every case sent, byte for byte (`curl -sS U`):

```
OPTIONS * RTSP/1.0\r\n
CSeq: 1\r\n
User-Agent: curl/8.21.0\r\n
\r\n
```

The URL's path is never sent: the Request-URI is `*` whatever the URL names (`rtsp://.../`,
`.../media`, `.../media/a.sdp` alike). `-e` adds `Referer` after `CSeq`, `-A` replaces the
`User-Agent`, `-H` fields follow it, `-u` adds `Authorization` last.

| Case | Server's reply (each echoing `CSeq: {CSEQ}` unless said) | curl arguments | Exit | stdout, stderr, and what curl sent |
| --- | --- | --- | --- | --- |
| plain | `200 OK`, `Server: surl`, `Public: OPTIONS, DESCRIBE, ...` | `-sS U` | 0 | stdout empty |
| fields | the same | `-sS -v -H "X-Test: 1" -A surl-test/1 -e http://ref/ U/a.sdp` | 0 | `Referer: http://ref/`, `User-Agent: surl-test/1`, `X-Test: 1`; `-v` shows both heads, `Connection #0 ... left intact` |
| `-i` | the same | `-sS -i U` | 0 | stdout is the response head as received |
| `Date` and `Content-Length: 0` | `200`, `Date`, `Server`, `Public`, `Content-Length: 0` | `-sS -w %{response_code} U` | 0 | `200` |
| body on a `200` | `200`, `Content-Length: 5`, `hello` | `-sS -i -w "[%{response_code} %{size_download} %{size_header}]" U` | 0 | the head, then `[200 0 73]`: **the body is not written**; `-v` shows `shutting down connection #0` |
| wrong `CSeq` | `200`, `CSeq: 7` | `-sS U` | 85 | `curl: (85) The CSeq of this request 1 did not match the response 7` |
| no `CSeq` | `200` without `CSeq` | `-sS U` | 85 | `... did not match the response 0` |
| `cseq` in lower case | `200`, `cseq: 1` | `-sS U` | 0 | |
| `Session` on the answer | `200`, `Session: 12345678;timeout=60` | `-sS -v U` | 0 | |
| status without reason | `RTSP/1.0 200` | `-sS U` | 0 | |
| `RTSP/2.0` | `RTSP/2.0 200 OK` | `-sS U` | 8 | `curl: (8) Weird server reply` |
| an HTTP status line | `HTTP/1.1 200 OK` | `-sS U` | 52 | `curl: (52) Empty reply from server` |
| `404`, `501` | `RTSP/1.0 404 Not Found` | `-sS U` | 0 | nothing: like HTTP, a status is not an error without `-f` |
| `404`, `501` with `-f` | the same | `-sS -f U` | 22 | `curl: (22) The requested URL returned error: 404` (`501`) |
| `401` Basic, no credentials | `401`, `WWW-Authenticate: Basic realm="surl"` | `-sS U` | 0 | |
| `-u` | `401` Basic, then `200` | `-sS -u tester:secret U` | 0 | `Authorization: Basic dGVzdGVyOnNlY3JldA==` sent unasked on the first request; the `401` ends it (no retry) |
| `-u` refused | `401` | `-sS -f -u tester:wrong U` | 22 | `The requested URL returned error: 401` |
| `-u` against `403` | `403 Forbidden`, `Content-Length: 0` | `-sS -w %{response_code} -u tester:secret U` | 0 | `403` |
| `--digest` | `401` `Digest realm="surl", nonce="abc123", algorithm=MD5, qop="auth"`, then `200` | `-sS -v --digest -u tester:secret U` | 0 | the first request without `Authorization`; **on the same connection** `CSeq: 2` with `Authorization: Digest username="tester",realm="surl",nonce="abc123",uri="*",cnonce="...",nc=00000001,algorithm=MD5,response="...",qop="auth"`; `-v`: `Reusing existing rtsp: connection` |
| `--digest`, ADR-0032's three fields | `401` with `Digest ... algorithm=MD5`, `... algorithm=SHA-256`, `Session: 0123456789ABCDEF;timeout=60`, `Content-Length: 0`, then `200` | `-sS --digest -u tester:secret U` | 0 | MD5 answered; `Session: 0123456789ABCDEF` (no `timeout`) sent on the retry |
| `--digest` refused | `401` Digest twice, `Content-Length: 0` | `-sS -w %{response_code} --digest -u tester:wrong U` | 0 | `401`, after one retry |
| `--digest`, body on the `401` | `401` Digest with `Content-Length: 12` and `Unauthorized`, then `200` | `-sS -v -m 5 --digest -u tester:secret U` | 28 | `Ignoring the response-body`, `shutting down connection #0`: **curl drops the connection and retries on a new one** (the one-connection recorder never answered it) |
| `--anyauth` | `401` with `Digest` and `Basic` | `-sS --anyauth -u tester:secret U` | 0 | Digest chosen |
| `Session` changed | `401` with `Session: AAA`, then `200` with `Session: BBB` | `-sS --digest -u tester:secret U` | 86 | `curl: (86) Got RTSP Session ID Line [BBB` `], but wanted ID [AAA]` |
| `Session` kept | `Session: AAA` on both | the same | 0 | the retry carries `Session: AAA` |
| `--oauth2-bearer` | `200` | `-sS --oauth2-bearer tok U` | 0 | `Authorization: Bearer tok` sent unasked |
| `--ntlm` | `401 WWW-Authenticate: NTLM` | `-sS --ntlm -u tester:secret U` | killed | `Authorization: NTLM TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==` (type 1) sent unasked |
| `-T` | `200` | `-sS -T up.sdp U` | 0 | **nothing of the file is sent**: the same `OPTIONS` |
| `-d`, `-X DESCRIBE`, `-I` | `200` | `-sS -d x=1 U`, `-sS -X DESCRIBE U`, `-sS -I U` | 0 | the same `OPTIONS` each time: none of them reaches RTSP |

What this settles:

1. **The tool sends one `OPTIONS *` and nothing else.** No path, no body, no other method, whatever
   the options. So against the tool the server's only observable behaviour is its `OPTIONS` answer,
   its logins, and its limits; everything else is libcurl's (decision 12).
2. **curl checks `CSeq` (85), `Session` (86) and the status line (8 for another version).** It
   reads `CSeq` case-insensitively and accepts a `Session` with `;timeout=`, sending the ID alone.
3. **curl keeps the connection when a response has no body** and reuses it for Digest's second
   leg; a body on a response it does not want makes it drop the connection and reconnect.
4. **Logins are HTTP's.** Basic and Bearer go unasked; Digest answers a `401` once, with
   `uri="*"`; a `4xx` is exit 0 without `-f` and 22 with it.

**Not measured:** the Linux build (the lane runs Windows only; it is the same tag's `lib/rtsp.c`,
and BL-318 runs every case there through CI); and every libcurl-only request, which waits for the
driver (decision 12).

## Decision

### 1. The response head

Every response is written with ADR-0070's `HttpResponseHead` for `HttpMessageProtocol.Rtsp10`:

```
RTSP/1.0 <status> <reason>\r\n
CSeq: <the request's CSeq, byte for byte>\r\n
Date: <IMF-fixdate from the injected TimeProvider>\r\n
Server: surl\r\n
<the fields the request's answer names>\r\n
Content-Length: <n>\r\n          (only when there is a body)
\r\n
```

- `CSeq` comes first because RFC 2326 section 12.17 requires it on every response and curl fails
  85 without it (measured). It is copied from the request; surl never invents one.
- `Date` and `Server` as the HTTP server has them (ADR-0019; measured: curl accepts both).
- **No body except where the request asks for one** - `DESCRIBE`'s description only. Every other
  response, refusals included, has no body and no `Content-Length` (RFC 2326 section 12.14: absent
  is zero). curl writes no body but `DESCRIBE`'s and `GET_PARAMETER`'s (`lib/rtsp.c`; measured for `OPTIONS`), and a body it
  does not want costs it the connection.
- **The connection stays open** after every response except the closing refusals of decision 2.

### 2. Each refusal, and the order requests are judged in

The first check that fails answers. "Closes" means the response is written within ADR-0019's
one-second refusal deadline and the connection is then closed as the HTTP server closes a refusal.

| # | Check | Refusal |
| --- | --- | --- |
| 1 | The head arrives within `--head-timeout` and `--max-request-head`, and parses with ADR-0070's reader for `RTSP/1.0` | `408 Request Time-out`, `431 Request Header Fields Too Large`, `400 Bad Request` (closes, no `CSeq`: none was read); a version other than `RTSP/1.x` is `505 RTSP Version Not Supported` (closes) |
| 2 | Exactly one `CSeq`, a decimal number of 1 to 9 digits | `400 Bad Request`, no `CSeq` field; the connection stays open |
| 3 | No `Transfer-Encoding` (RFC 2326 has no chunked bodies), and a `Content-Length` no larger than `--max-filesize` | `400 Bad Request` / `413 Request Entity Too Large`, both closing, the body unread |
| 4 | The login (decision 7) | `401 Unauthorized` with the challenges, or `403 Forbidden` |
| 5 | The method is one of the ten | `501 Not Implemented` (`GET`, `REDIRECT` and every other token alike) |
| 6 | The Request-URI is `*` or an absolute `rtsp://` URL | `400 Bad Request` |
| 7 | The method's own checks (decisions 3 to 6) | as each says |

The request body, when the head announced one, is read before the answer to checks 4 to 7 is
written, so the next head starts where curl expects; a body not needed (any method but `ANNOUNCE`,
`GET_PARAMETER` and `SET_PARAMETER`) is read and discarded.

**Why `431`, `408`, `413` and `503` though RFC 2326 predates `431`:** ADR-0006 section 5 already
decided them for "HTTP, RTSP", RTSP's statuses extend HTTP's (RFC 2326 section 7.1.1), and curl
treats every status alike (measured). **Why the login before `501`:** ADR-0032 section 4's
reason: a client that may not connect learns nothing about the server.

**The Request-URI.** An absolute URL's host and port are not checked (the listener already chose
the server, as the HTTP server ignores a mismatched `Host`); its path, percent-decoded, names the
**presentation**, a file in the content store after ADR-0006 section 2's exposure checks. `*` names
none: it serves `OPTIONS`, `GET_PARAMETER` and `SET_PARAMETER`, and `PLAY`, `PAUSE`, `TEARDOWN` and
`RECORD` that carry a `Session` (the session names the presentation); `DESCRIBE`, `ANNOUNCE` and
`SETUP` with `*` are `400 Bad Request`. libcurl sends `*` unless `CURLOPT_RTSP_STREAM_URI` is set.

### 3. `OPTIONS`

`200 OK` with `Public: OPTIONS, DESCRIBE, ANNOUNCE, SETUP, PLAY, PAUSE, TEARDOWN, GET_PARAMETER,
SET_PARAMETER, RECORD` - all ten, always, because the server implements all ten; whether an upload
is allowed is answered by the upload itself (decision 6), so `Public` does not say whether
`--allow-uploads` is on. No lookup: `OPTIONS` for a path that does not exist is `200` too (and the
tool never sends a path, measured). A `Session` it carries is checked as decision 5 says and named
back.

### 4. `DESCRIBE`: what a presentation is

**Every file is a presentation of one stream whose RTP payloads are the file's bytes, in order.**
surl has no media codecs and the tool cannot ask for media; a file streamed as opaque octets
reaches a libcurl client's interleave callback byte for byte, so a download through RTSP is
provable, and a file recorded with `RECORD` plays back as it arrived (decision 6). `DESCRIBE` of a
presentation path answers `200` with:

```
Content-Type: application/sdp
Content-Base: <the Request-URI, as sent>
Content-Length: <n>

v=0\r\n
o=- 0 0 IN IP4 <the connection's local address>\r\n          (IP6 for IPv6)
s=<the file's name>\r\n
c=IN IP4 0.0.0.0\r\n                                          (IP6 ::)
t=0 0\r\n
a=control:*\r\n
a=range:npt=0-\r\n
m=application 0 RTP/AVP 96\r\n
a=rtpmap:96 octet-stream/90000\r\n
a=control:*\r\n
```

RFC 4566 SDP: session ID and version 0 (the description never changes while served, and RFC
4566 allows any numeric value), the file name escaped as ADR-0006 section 3 escapes peer-visible
names (control characters dropped, UTF-8 kept), port 0 because the transport is negotiated in
`SETUP` (RFC 2326 appendix C.1), the dynamic payload type 96 named after the
`application/octet-stream` media type, and `a=control:*` so the stream's control URL is the
presentation's own (RFC 2326 appendix C.1.1): there is one stream, and no aggregate-versus-stream
distinction. `Accept` is ignored: SDP is the only description surl writes.

A path that is missing, hidden or a directory is `404 Not Found` (no listing over RTSP: a directory
is not a presentation).

### 5. Sessions, `SETUP`, `PLAY`, `PAUSE`, `TEARDOWN`, and the parameters

**Sessions.** A `SETUP` without `Session` creates one: the ID is **16 upper-case hexadecimal digits
from 8 bytes of the injected random source** (RFC 2326 section 12.37 asks for at least 8 octets,
unguessable). A session belongs to its connection - its transport is that connection (below) - and
ends when the connection ends; **one session per connection**, so a `SETUP` without `Session` on a
connection that holds one is `455 Method Not Valid in This State`. The `SETUP` answer carries
`Session: <id>;timeout=60`, every later answer on the session `Session: <id>` (measured: curl
accepts both and compares the ID only). A session that is neither playing nor recording and gets no
request for **60 seconds** (RFC 2326's default, measured from the injected `TimeProvider`) ends;
any request naming an ID the connection does not hold, ended or never made, is `454 Session Not
Found`.

**States** (RFC 2326 appendix A): `Ready` after `SETUP`; `Playing` after `PLAY`; `Recording` after
`RECORD`. In a state where a request has no meaning - `PLAY` on a session set up to record,
`RECORD` on one set up to play, `SETUP` while `Playing` or `Recording` - the answer is `455 Method
Not Valid in This State`. `PAUSE` in `Ready` is `200` (nothing to pause). A path on `PLAY`, `PAUSE`,
`RECORD` or `TEARDOWN` other than `*` or the session's presentation is `404 Not Found`.

**`SETUP`** needs `Transport` (`400 Bad Request` without one; curl refuses to send it without one
anyway). Of its comma-separated alternatives, the first that is `RTP/AVP/TCP` and not `multicast`
is taken; none is `461 Unsupported Transport`. So **UDP (`RTP/AVP`, `RTP/AVP/UDP`) and multicast are
refused with `461`**: libcurl's receive reads only interleaved data (`RTSPREQ_RECEIVE`), so no
upstream curl client can take RTP over UDP, and interleaving keeps the server on the listener seam's
one connection. `interleaved=<n>-<m>` is honoured when `n` is even, `m` is `n + 1` and both are below
256; absent, surl takes `0-1`; otherwise `461`. The presentation must exist (`404`). `mode=record`
(quoted or not, any case) sets the session up to record (decision 6); any other `mode` but `play`
is `461`. The answer: `Transport: RTP/AVP/TCP;unicast;interleaved=<n>-<m>;ssrc=<8 hex digits from
the random source>` (plus `;mode=record` when recording). A `SETUP` with `Session` in `Ready`
re-negotiates the transport of that session.

**`PLAY`** opens the presentation (`404` if it has gone), answers `200` with `Range: npt=0-` and
`RTP-Info: url=<the Request-URI>;seq=<first sequence number>;rtptime=<first timestamp>`, then
streams it on the interleaved RTP channel `n`:

- each frame is `$`, the channel byte, a two-byte big-endian length, then one RTP packet (RFC 2326
  section 10.12);
- each packet is RFC 3550 section 5.1's 12-byte header - version 2, no padding, no extension, no
  CSRC, marker set on the last packet only, payload type 96, a sequence number starting at a
  random 16-bit value, a timestamp starting at a random 32-bit value and advancing by each
  payload's length, the session's SSRC - and **1400 payload bytes** (the last packet fewer; an
  empty file sends one packet with no payload and the marker);
- after the last packet, one frame on channel `m` carrying an RTCP compound packet (RFC 3550
  sections 6.4.1 and 6.6): a sender report (`80 C8 00 06`, the SSRC, the NTP time from the
  `TimeProvider`, the last RTP timestamp, the packet and octet counts) then a `BYE` (`81 CB 00 01`
  and the SSRC). The session is then `Ready`, its position back at the start, so another `PLAY`
  plays the file again.

**Pace: as fast as the connection takes it.** The bytes carry no timing, and pacing would only make
a download slower; the idle timeout already ends a client that stops reading. Between frames surl
reads whatever request has arrived and answers it in place (RTSP responses and `$` frames share the
connection, RFC 2326 section 10.12). `Range` other than absent, `npt=0-` or `npt=now-` is
`457 Invalid Range` (the octets have no other time to seek to).

**`PAUSE`** while `Playing` stops after the frame being written, answers `200`, and keeps the
position: the next `PLAY` resumes with the next packet, its sequence number and timestamp following
on. **`TEARDOWN`** ends the session and answers `200` with no `Session` field; the connection stays
open.

**`GET_PARAMETER`** with no body is `200` with no body - the keep-alive RFC 2326 section 10.8
describes; it refreshes the session's 60 seconds when it names one. **`GET_PARAMETER` or
`SET_PARAMETER` with a body** is `451 Parameter Not Understood`: surl has no parameters to report
or set. `SET_PARAMETER` with no body is `200`.

**Where this decision was first silent** (decided by Claude under Stewart's delegation in BL-315,
2026-09-30, and folded in here by BL-338):

- **A client that half-closes while its session plays is streamed the rest of the presentation**,
  and the connection then closes: a half-close says the client sends no more requests, not that it
  stops reading.
- **A `SETUP` naming the held session with another presentation's URL is `455 Method Not Valid in
  This State`**: one session holds one presentation, decision 4's single stream.
- **`PLAY` while `Playing` answers `200`** with the next packet's `RTP-Info` and carries on from the
  current position.
- **Every answer to a request that named the live session names it back**, refusals (`404`, `451`,
  `455`, `457`) included; `454` names none.
- **The 60-second timeout is checked when a request names the session** (and before a `SETUP` that
  names none) - the only moment a client can observe it - and never while playing or recording; the
  session's end is noted `ended: timeout` then.
- **The sender report's RTP timestamp is the last packet's; its packet and octet counts are the
  session's since `SETUP`**, across every `PLAY`.
- **A file that shrinks while playing, or can no longer be read, ends with an empty packet carrying
  the marker** where its bytes ran out; a file gone before `PLAY` is `404`.

### 6. `ANNOUNCE` and `RECORD`: uploads

Both are uploads (ADR-0006 section 2), refused unless `--allow-uploads`, through the content store's
upload session (ADR-0015) and bounded by `--max-filesize`:

- **`ANNOUNCE <path>`** with a body stores the body, byte for byte, as `<path>.sdp` - the description
  beside the media it describes, so a recording at `<path>` does not overwrite it. An empty body is
  `400 Bad Request`; the `Content-Type` is not checked (surl does not interpret SDP). `200` when
  stored.
- **`SETUP` with `mode=record`** is refused with `403 Forbidden` without `--allow-uploads`, before a
  session is made. The presentation need not exist.
- **`RECORD`** opens an upload session for the presentation's path and answers `200`; from then on
  every interleaved frame on the session's RTP channel is an RTP packet whose payload - after the
  header, the CSRC list, any extension and any padding (RFC 3550 section 5.1) - is appended in
  arrival order. A frame on the RTCP channel, or a packet that is not RTP version 2 or too short
  for its own header, is discarded. `PAUSE` stops appending, `RECORD` resumes it. **`TEARDOWN`
  commits the file** (a recording with no packets is an empty file: libcurl sends no RTP, so that is
  what a libcurl `RECORD` stores, measured from its source). The connection or session ending any
  other way deletes the partial file (ADR-0015).
- The store's refusals: uploads off, a hidden path, or a directory is `403 Forbidden` (one answer
  for all three, as ADR-0015 decides); a location that does not exist is `404 Not Found`.
- **Past `--max-filesize`:** an `ANNOUNCE` whose `Content-Length` is larger is `413` before its body
  is read (decision 2, check 3). A recording that grows past it is deleted, the session ends, and the
  connection is closed with ADR-0059's farewell window: there is no request to answer.

Interleaved frames that arrive outside a recording are read and discarded, bounded by their own
16-bit length.

### 7. Logins

Every request is judged through `IHttpAuthenticationSession` exactly as the HTTP server judges one
(ADR-0032 sections 4 and 6), with the `rtsp://` connection being plain text (curl has no `rtsps`,
ADR-0026 decision 3):

- **Reads need no login while no account is configured** - every request but `ANNOUNCE`, `RECORD` and
  a `SETUP` with `mode=record`, which are writes. Once any account is configured, **every request
  needs one, `OPTIONS` included** - the tool's only request, and so the only way curl's `--digest`
  can log in. `--allow-anonymous` serves every request as anonymous. A write always needs a login
  and `--allow-uploads`.
- **`Basic` and `Bearer` are refused with `403 Forbidden` unchecked** without
  `--allow-plaintext-auth`, and with it checked (ADR-0032 section 4 step 2). Measured: curl sends
  `Basic` on the first request when given `-u`, and `-w %{response_code}` shows the `403`.
- The `401` carries one `WWW-Authenticate` per challenge value the session gives, in its order
  (ADR-0032 section 4: Digest's MD5, SHA-256 and SHA-512-256, and over plain text no Basic or Bearer
  unless `--allow-plaintext-auth`), and no body, so curl answers Digest on the same connection
  (measured). The Digest `uri` is the Request-URI as sent: `*` from the tool (measured).
- NTLM and Negotiate are offered when `--auth` enables them, as for HTTP: the session owns the
  connection-bound legs, and the connection persists across a `401`. BL-318 proves Basic, Bearer
  and Digest; NTLM against RTSP was not completed in measurement and is not in its list.
- The session's verdict and the login note follow ADR-0038, as the HTTP server writes them.

### 8. Limits (ADR-0006)

| Limit | RTSP behaviour |
| --- | --- |
| `--max-request-head` (100 KiB), `--head-timeout` (30 s) | the head, through ADR-0070's reader: `431` or `408`, closing (decision 2) |
| `--max-filesize` (100 MiB) | every request body: `413` before reading, closing; a recording: deleted and the connection closed (decision 6) |
| `--idle-timeout` (120 s) | no byte either way: close with no bytes (ADR-0006 section 5). Streaming is not idle; a client that stops reading is |
| `--max-time` (3600 s) | close with no bytes, a recording's partial file deleted |
| `--max-connections`, `--max-connections-per-address` | `RTSP/1.0 503 Service Unavailable` with no `CSeq` (no request was read), then close |
| Shutdown | close with no bytes (ADR-0059 decision 4), a recording's partial file deleted |
| Sessions | one per connection (decision 5), so the connection limits bound them |

### 9. Verbose notes and help

Notes are `IExchangeLog.Note` at `verbose` and above (ADR-0033 section 3), peer bytes escaped as
ADR-0006 section 3 says; every byte of every frame is in the engine's `--trace` dumps, with no note
per frame.

| When | Note |
| --- | --- |
| A refusal | `RTSP <method> refused: <status> <reason>: <check>`, e.g. `RTSP SETUP refused: 461 Unsupported Transport: no RTP/AVP/TCP alternative` |
| A session made or ended | `RTSP session <id> set up for <path>, interleaved <n>-<m>` / `RTSP session <id> ended: <TEARDOWN, timeout, connection closed>` |
| A `PLAY` finished or paused | `Streamed <bytes> bytes of <path> in <k> RTP packets` / `... paused after <k> packets` |
| An `ANNOUNCE` or recording stored | `Stored <bytes> bytes at <path>` |
| A recording discarded | `Recording of <path> discarded: <why>` |

**Help.** The category `rtsp`, `RTSP protocol` (curl 8.21.0 has no RTSP category to copy, as it
has none for SSH or WebSocket; ADR-0034 decision 1's wording), claims the scheme `rtsp`, and holds
every option the server reads: `--max-request-head`, `--head-timeout`, `--max-filesize`,
`--allow-uploads`, `--user`, `--user-file`, `--allow-anonymous`, `--allow-plaintext-auth`, `--auth`
and `--directory`. No new option. The `--aihelp` topic is `rtsp`; its prose says the tool sends
only `OPTIONS *`, that every file is a one-stream presentation of its bytes, and that only
interleaved TCP transport is served. BL-317 adds the category, the topic's prose and example, and
grows the pinned topic lists, as root `CLAUDE.md` requires.

### 10. The scaffold's error

`Surl.Protocol.Rtsp.UnitLibrary/CLAUDE.md` named "upstream curl's `--rtsp-request`" as the source of
the requests. curl 8.21.0 has no such option: it is libcurl's `CURLOPT_RTSP_REQUEST`. This task
corrected it.

### 11. What BL-318 proves with the pinned build

Against `surl` over loopback through the conformance harness, on Windows and Linux; on macOS each
case reports Inconclusive (ADR-0026). `P` is a temporary `--directory` holding `clip.bin`; `up.sdp`
is a small file beside the test.

| surl options | curl command line | Exit, and what is checked |
| --- | --- | --- |
| `-d P` | `curl -sS rtsp://127.0.0.1:<p>/` | 0, stdout empty |
| `-d P` | `curl -sS -i rtsp://.../clip.bin` | 0, stdout is `RTSP/1.0 200 OK`, `CSeq: 1`, `Date`, `Server: surl`, decision 3's `Public` |
| `-d P` | `curl -sS -v -H "X-Test: 1" -A agent/1 rtsp://.../missing` | 0 (`OPTIONS` looks nothing up); stderr shows `> OPTIONS * RTSP/1.0` |
| `-d P` | `curl -sS -w "%{response_code}" rtsp://.../clip.bin` | 0, stdout `200` |
| `-d P --allow-uploads` | `curl -sS -T up.sdp rtsp://.../up.sdp` | 0, and `P` holds no `up.sdp` (the tool sends no body) |
| `-d P --user tester:secret` | `curl -sS -f rtsp://.../clip.bin` | 22 (`401`) |
| `-d P --user tester:secret` | `curl -sS -f -u tester:secret rtsp://.../clip.bin` | 22 (`403`: Basic in plain text) |
| `-d P --user tester:secret` | `curl -sS -f --basic -u tester:secret rtsp://.../clip.bin` | 22 (`403`) |
| `-d P --user tester:secret --allow-plaintext-auth` | `curl -sS -f -u tester:secret rtsp://.../clip.bin` | 0 |
| `-d P --user tester:secret --allow-plaintext-auth` | `curl -sS -f -u tester:wrong rtsp://.../clip.bin` | 22 (`401`) |
| `-d P --user tester:secret` | `curl -sS -f --digest -u tester:secret rtsp://.../clip.bin` | 0, on one connection |
| `-d P --user tester:secret` | `curl -sS -f --digest -u tester:wrong rtsp://.../clip.bin` | 22 (`401` after one retry) |
| `-d P --user tester:secret` | `curl -sS -f --anyauth -u tester:secret rtsp://.../clip.bin` | 0 (Digest) |
| `-d P --user :tok --allow-plaintext-auth` | `curl -sS -f --oauth2-bearer tok rtsp://.../clip.bin` | 0 |
| `-d P --user tester:secret --allow-anonymous` | `curl -sS -f rtsp://.../clip.bin` | 0 |
| `-d P --max-request-head 128` | `curl -sS -f -H "X-Long: <200 x>" rtsp://.../clip.bin` | 22 (`431`) |

Exit codes 85 and 86 cannot be provoked by a correct server; `Surl.Protocol.Rtsp.UnitTests` pins
that every response echoes `CSeq` and keeps one `Session` ID.

Plus, once decision 12's tasks land, through the pinned `libcurl-4.dll` (Windows only): `DESCRIBE`
of `clip.bin` answered with decision 4's SDP; `SETUP` with `RTP/AVP/TCP;interleaved=0-1` answered
with a `Session`; `PLAY` then `RTSPREQ_RECEIVE` delivering packets whose payloads join to
`clip.bin`, then the RTCP `BYE`; `PAUSE`, `GET_PARAMETER` and `TEARDOWN` answered `200` with the same
`Session`; `SETUP` with `RTP/AVP;unicast;client_port=5000-5001` answered `461`; a made-up `Session`
answered `454`; with `--allow-uploads`, `ANNOUNCE` stored as `<path>.sdp`, and `SETUP` with
`mode=record`, `RECORD`, `TEARDOWN` leaving an empty file - with the libcurl results the measuring
task records.

### 12. libcurl: adopt ADR-0071's pin

ADR-0071 decision 10 pinned the reference build's `libcurl-4.dll` (BL-323 pins it and builds a
driver for `curl_ws_send` and `curl_ws_recv`). RTSP needs it more: every request but `OPTIONS` is
libcurl's. This ADR adopts the pin - no further download or question - and files:

- **BL-332** (feature, after BL-323): the driver also runs RTSP - `CURLOPT_RTSP_REQUEST`,
  `CURLOPT_RTSP_STREAM_URI`, `CURLOPT_RTSP_TRANSPORT`, `CURLOPT_RTSP_SESSION_ID`, the upload body for
  `ANNOUNCE`, `RTSPREQ_RECEIVE` with `CURLOPT_INTERLEAVEFUNCTION` - printing each `CURLcode`, status,
  `Session` and the interleaved bytes.
- **BL-333** (docs, after BL-332): measures decision 11's libcurl cases through it against the
  recorder and amends this ADR with the requests libcurl sends and what it does with decision 4 and
  5's answers. A measurement that contradicts a decision amends the decision.
- BL-318 depends on BL-333. Until then, BL-313 to BL-316 use RFC 2326 section 14's request
  examples as fixtures beside the recorded `OPTIONS` requests.

## Alternatives considered

- **Serve `.sdp` files from the content store as `DESCRIBE`'s answer**, and stream nothing or a
  synthetic stream. Rejected: surl has no codecs to stream what such a description names, so the
  description would promise media that never arrives; describing every file as its own bytes keeps
  the description true.
- **UDP transport.** Rejected: no upstream curl client can receive it (libcurl reads only
  interleaved data), and it would need a datagram seam for a server no curl uses. `461` is RFC
  2326's answer for a transport the server does not support.
- **Pace `PLAY` in real time.** Rejected: the payloads have no time base; pacing slows a download
  and proves nothing.
- **List only the eight methods in `Public` without `--allow-uploads`.** Rejected: `Public` says
  what the server implements; the upload's own `403` says what is allowed, as ADR-0015 keeps
  uploads-off and hidden alike.
- **Sessions that outlive their connection.** Rejected: with interleaved transport the session's
  media is that connection; RFC 2326 section 10.12 lets the session end with it.

## Consequences

- BL-313 builds decisions 1 to 4 and 8's head limits; BL-314 decision 7; BL-315 decision 5 and 8's
  session rows; BL-316 decision 6; BL-317 decision 9's help; BL-318 decision 11.
- `Record-CurlExchange.ps1 -Raw` answers RTSP through `{CSEQ}`, so the `OPTIONS` fixtures of
  BL-313 and BL-314 (the plain request, the `-H`, `-A`, `-e` fields, the Basic header and both
  Digest legs) are recorded with it.
- BL-332 and BL-333 are filed and BL-318 depends on BL-333.
- If the Linux build or the libcurl measurements disagree with a decision, the finding amends it here.

## Amendment 1 - libcurl's RTSP requests, measured (BL-333, 2026-09-30)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30.

Measured 2026-09-30 on Windows with the pinned `C:\Program Files\Git\mingw64\bin\libcurl-4.dll`,
SHA-256 `799F7EEFC3C9DA9C80EC5AEA221A02B3AFE2C5350C6B45FD5A4865E7E2D4E574` (kind library in
`UpstreamCurlBuilds.json`), driven by BL-332's `Run-LibcurlRtspScript.cs` through
`Record-CurlExchange.ps1 -Raw -RawIdleMilliseconds 400 -LibcurlRtsp`, one `-RawReply` per request,
each echoing `CSeq: {CSEQ}` and carrying `Date` and `Server: surl` as decision 1 writes them. The
play case was:

```
.\Record-CurlExchange.ps1 -Port 18554 -Raw -RawIdleMilliseconds 400 -LibcurlRtsp -RawReply <replies> -CurlArgs '--timeout','3000','rtsp://127.0.0.1:18554/clip.bin','stream-uri:rtsp://127.0.0.1:18554/clip.bin','transport:RTP/AVP/TCP;interleaved=0-1','SETUP','PLAY','RECEIVE','TEARDOWN' -OutDirectory <dir>
```

The other cases change only the replies and the steps. `S` is the URL
`rtsp://127.0.0.1:18554/clip.bin` (`.../rec.bin` for the uploads), set with `stream-uri:`; `ID` is
the session `0123456789ABCDEF` the `SETUP` reply named, with `;timeout=60`. The driver sets no
`CURLOPT_USERAGENT`, so no request carries `User-Agent`.

**The requests libcurl sends**, byte for byte (each head ends `\r\n\r\n`, every line `\r\n`):

| Request | Bytes |
| --- | --- |
| `DESCRIBE` | `DESCRIBE S RTSP/1.0`, `CSeq: 1`, `Accept: application/sdp` |
| `SETUP` | `SETUP S RTSP/1.0`, `CSeq: n`, `Transport: <CURLOPT_RTSP_TRANSPORT as set>`; plus `Session: ID` once libcurl holds one |
| `PLAY`, `PAUSE`, `RECORD`, `TEARDOWN` | `<METHOD> S RTSP/1.0`, `CSeq: n`, `Session: ID` - nothing else; **`RECORD` sends no body and no RTP** |
| `GET_PARAMETER` without a body | `GET_PARAMETER S RTSP/1.0`, `CSeq: n`, `Session: ID` |
| `GET_PARAMETER` with `body:volume\r\n` | the same, then `Content-Length: 8`, `Content-Type: text/parameters`, the 8 bytes |
| `ANNOUNCE` with `body:v=0\r\n` (`CURLOPT_COPYPOSTFIELDS`) | `ANNOUNCE S RTSP/1.0`, `CSeq: n`, `Content-Length: 5`, `Content-Type: application/sdp`, the 5 bytes |
| `ANNOUNCE` with `upload:` (`CURLOPT_UPLOAD`, 10 bytes) | the same with `Content-Length: 10` |
| `OPTIONS` | `OPTIONS S RTSP/1.0`, `CSeq: n`, and `Session: ID` once libcurl holds one |

**The Request-URI** is `CURLOPT_RTSP_STREAM_URI` as set, absolute; without it every request line
is `<METHOD> * RTSP/1.0`, whatever `CURLOPT_URL` names (`SETUP * RTSP/1.0` included), as BL-332
measured. Decision 2's `400` for `DESCRIBE`, `ANNOUNCE` and `SETUP` with `*` stands: a libcurl
client must name the stream, and BL-318 sets `stream-uri:` in every libcurl case.

**What libcurl does with decision 4 and 5's answers:**

| Case | Steps | Server's reply | What the driver printed |
| --- | --- | --- | --- |
| describe | `DESCRIBE` | `200`, `Content-Type: application/sdp`, `Content-Base: S`, `Content-Length: 170`, decision 4's SDP for `clip.bin` | `CURLcode 0`, status 200, the 170 SDP bytes in the write callback, session none |
| setup | `transport:RTP/AVP/TCP;interleaved=0-1`, `SETUP` | `200`, `Session: ID;timeout=60`, `Transport: RTP/AVP/TCP;unicast;interleaved=0-1;ssrc=AABBCCDD` | `CURLcode 0`, status 200, session `ID` (no `timeout`) |
| frames with the `PLAY` answer | `PLAY`, `RECEIVE` | `200`, `Session: ID`, `Range: npt=0-`, `RTP-Info: ...`, then in the same write two RTP frames on channel 0 (`$ 00 00 10`, 12-byte header, 4 payload bytes) and one RTCP `BYE` frame on channel 1 (`$ 01 00 08 81 CB 00 01` + SSRC) | `PLAY`: `CURLcode 0`, status 200, **the interleave callback called three times during `PLAY`'s own perform**, once per frame, each with the whole frame from `$` on; `RECEIVE` then had nothing to read |
| frames after the `PLAY` answer | `PLAY`, `RECEIVE` | `PLAY`'s head alone; once it paused, the three frames | `PLAY`: interleaved none; `RECEIVE`: `CURLcode 0`, status 0, the same three calls |
| frames before the `PAUSE` answer | `PLAY`, `PAUSE` | two RTP frames, then the `200` with `Session: ID`, in one write | `PAUSE`: `CURLcode 0`, status 200, the two frames in the interleave callback |
| `TEARDOWN` | `TEARDOWN` | `200` with no `Session` | `CURLcode 0`, status 200; **libcurl keeps `ID`** |
| after `TEARDOWN` | `OPTIONS`, `SETUP` | `200` with no `Session`; then `200` with `Session: 1111111111111111;timeout=60` | both requests **carry `Session: ID`**; `OPTIONS` `CURLcode 0`; `SETUP` **`CURLcode 86` (RTSP session error)** |
| UDP refused | `transport:RTP/AVP;unicast;client_port=5000-5001`, `SETUP` | `461 Unsupported Transport` | `CURLcode 0`, status 461, session none |
| wrong state | `RECORD` on a play session | `455 Method Not Valid in This State`, `Session: ID` | `CURLcode 0`, status 455 |
| parameter with a body | `body:volume\r\n`, `GET_PARAMETER` | `451 Parameter Not Understood`, `Session: ID` | `CURLcode 0`, status 451 |
| made-up session | `session-id:DEADBEEF`, `PLAY` | `454 Session Not Found`, no `Session` | `CURLcode 0`, status 454 |
| `ANNOUNCE` refused, then stored | `body:v=0\r\n`, `ANNOUNCE`, `ANNOUNCE` | `403 Forbidden`; then `200` | `CURLcode 0`, status 403; then 200 |
| record | `transport:RTP/AVP/TCP;unicast;interleaved=0-1;mode=record`, `SETUP` twice, `RECORD`, `TEARDOWN` | `403`; then `200` with `Session: ID;timeout=60` and `Transport: ...;mode=record`; `200`; `200` | each `CURLcode 0`: 403, then 200 with session `ID`, 200, 200 |
| without a session | `SET_PARAMETER` before any `SETUP` | none: nothing is sent | `CURLcode 43`, BL-332's measurement |

What this settles:

1. **Every refusal decision 5 and 6 name - `403`, `451`, `454`, `455`, `461` - is `CURLcode 0` with
   the status**: libcurl, like the tool, treats a status as no error. A refusal with no `Session`
   field (`454`, `461`, `403` on `SETUP`) is accepted. Decisions 1 to 6 stand as written, except
   decision 5's handling of a torn-down session below.
2. **Interleaved frames are delivered whole, one callback per frame, by whichever perform reads
   them** - `PLAY`'s when they follow its answer in the same read, `PAUSE`'s when they precede its
   answer, `RECEIVE`'s otherwise. So decision 5's unpaced streaming and in-place answers suit
   libcurl: no frame is lost however the bytes fall.
3. **`DESCRIBE` sends `Accept: application/sdp`**, which decision 4's SDP satisfies; **`ANNOUNCE`
   sends `Content-Type: application/sdp`** and the parameter bodies `text/parameters`, which
   decision 5 and 6 do not check.
4. **libcurl keeps the session ID after `TEARDOWN` and sends it on every later request on the
   handle**, and fails 86 when a later `SETUP` answer names another ID. Decision 5 as written would
   answer each of those requests `454`, and a second session on the handle could never be made.

**Decision 5 amended: a torn-down session's ID stays the connection's.** When `TEARDOWN` ends a
session, the connection remembers its ID. A later request naming that ID is judged as if it named
no session - `OPTIONS`, `DESCRIBE`, `ANNOUNCE`, `GET_PARAMETER` and `SET_PARAMETER` are served, and
the answer carries no `Session` field - except that `PLAY`, `PAUSE`, `RECORD` and `TEARDOWN`, which
need a live session, are still `454 Session Not Found`, and **a `SETUP` naming it makes the new
session under that same ID** (answered `Session: ID;timeout=60`), because libcurl compares the ID
and fails 86 on any other. Reusing the ID costs nothing RFC 2326 section 12.37 protects: the ID is
bound to the one connection that already holds it. An ID the connection never held, or one ended
by the 60-second timeout or a limit, is `454` as before. Why not clear it in the client: libcurl's
only way is resetting `CURLOPT_RTSP_SESSION_ID`, which no RTSP client knows to do, and the server is
the mate of the client as it is.

**Decision 11's libcurl cases, with the expected libcurl results** (every one through the pinned
`libcurl-4.dll`, Windows only, `stream-uri:` set to the case's file, `P` holding `clip.bin`):

| surl options | Driver steps | Expected |
| --- | --- | --- |
| `-d P` | `DESCRIBE` | `CURLcode 0`, status 200, body decision 4's SDP for `clip.bin` |
| `-d P` | `transport:RTP/AVP/TCP;interleaved=0-1`, `SETUP` | `CURLcode 0`, status 200, a 16-hex-digit session |
| `-d P` | the same, `PLAY`, `RECEIVE` | `PLAY` and `RECEIVE` `CURLcode 0`, status 200 then 0; the payloads after each frame's 12-byte RTP header, from every interleave call of both steps on channel 0, join to `clip.bin`; the last channel-1 call ends with `81 CB 00 01` and the SSRC |
| `-d P` | after `PLAY`: `PAUSE`, `GET_PARAMETER`, `TEARDOWN` | each `CURLcode 0`, status 200, the same session |
| `-d P` | after `TEARDOWN`: `OPTIONS`, `SETUP` | `CURLcode 0`, status 200 each; `SETUP`'s session the same ID (amended decision 5) |
| `-d P` | `transport:RTP/AVP;unicast;client_port=5000-5001`, `SETUP` | `CURLcode 0`, status 461, session none |
| `-d P` | `session-id:DEADBEEF`, `PLAY` | `CURLcode 0`, status 454 |
| `-d P` | `SETUP` to play, `RECORD` | `CURLcode 0`, status 455 |
| `-d P` | `SETUP`, `body:volume\r\n`, `GET_PARAMETER` | `CURLcode 0`, status 451 |
| `-d P` | `body:v=0\r\n`, `ANNOUNCE` | `CURLcode 0`, status 403 (uploads off), `P` holds no `rec.bin.sdp` |
| `-d P --allow-uploads` (and an account where decision 7 needs one) | `body:v=0\r\n`, `ANNOUNCE` | `CURLcode 0`, status 200, `P/rec.bin.sdp` is the 5 bytes |
| the same | `no-body`, `transport:RTP/AVP/TCP;unicast;interleaved=0-1;mode=record`, `SETUP`, `RECORD`, `TEARDOWN` | each `CURLcode 0`, status 200, and `P/rec.bin` an empty file |

**Follow-up.** BL-313 and BL-314 are done with RFC 2326 section 14's `DESCRIBE` example as their
fixture (`Surl.Protocol.Rtsp.UnitTests/Fixtures/rfc2326-describe`), and BL-315 is in progress
against decision 5 as first written; BL-337 re-pins the fixtures to the requests above and builds
the amended decision 5, and BL-318 depends on it. BL-316, not yet started, takes its fixtures from
the `ANNOUNCE`, `SETUP` `mode=record`, `RECORD` and `TEARDOWN` rows above.
