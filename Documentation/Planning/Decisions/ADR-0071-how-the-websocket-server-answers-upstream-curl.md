# ADR-0071 — How the WebSocket server answers upstream curl

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-285 (FR-048).
- **Amends:** nothing. [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md)'s WebSocket rows,
  [ADR-0010](ADR-0010-the-server-side-tls-contract.md) (`wss`),
  [ADR-0019](ADR-0019-how-the-http-server-enforces-the-hardening-limits.md) (`Server: surl`),
  [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) section 4 (HTTP
  policy) and section 6 (the contract), [ADR-0059](ADR-0059-how-a-protocol-server-tells-a-limit-from-shutdown.md)
  (limit or shutdown) and [ADR-0070](ADR-0070-the-http-message-library-the-http-websocket-and-rtsp-servers-share.md)
  (the upgrade head is read and written with `Surl.HttpMessage`) are applied as written. No contract
  in `Surl.Protocol.Abstractions` changes.

## Context

`surl ws://...` and `surl wss://...` are to be the server upstream curl's `ws://` and `wss://`
transfers talk to (FR-048). BL-288 builds the RFC 6455 frame codec and the accept key from the RFC
alone; ADR-0070 decided the library the upgrade head is read and written with. What was left, and
what this ADR decides from measurement, is every byte `Surl.Protocol.Ws` sends around the codec:
the answer to curl's upgrade `GET` and each refusal, the logins, what surl sends once upgraded, how
it answers every frame a client can send, ADR-0006's limits in WebSocket's words, `wss`, the
verbose notes and the help category, and the libcurl question, so that BL-301, BL-302 and BL-303
are built without a question and BL-304 knows what to prove.

**What the `curl` tool does, from upstream's own documents** (tag `curl-8_21_0`,
`docs/internals/WEBSOCKET.md`, read 2026-09-30): the upgrade is made over HTTP/1.1; an answer other
than `101` is `CURLE_HTTP_RETURNED_ERROR` (22); libcurl answers a `PING` with a `PONG` unless
`CURLWS_NOAUTOPONG`; and "Command line tool WebSocket ... has not been started". Neither
`docs/cmdline-opts` nor `src/tool_listhelp.c` at the tag has a WebSocket option. So the tool
upgrades and writes what it receives; only libcurl's API (`curl_ws_send`, `curl_ws_recv`,
`CURLOPT_CONNECT_ONLY` 2) sends data, fragmented, ping and close frames of the caller's choosing.

### What upstream curl 8.21.0 does (measured)

Measured 2026-09-30 with the Windows reference build, `C:\Program Files\Git\mingw64\bin\curl.exe`,
SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`
(`curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel ...`), through
`Record-CurlExchange.ps1 -Raw`, extended by this task so a reply's `{WS_ACCEPT}` becomes the accept
value of the key curl sent (the script's help, `-RawReply`). Each case gave one scripted reply (a
head and server frames written in `\xHH`) and, where noted, a second one after curl's next pause.
`H` below is `HTTP/1.1 101 Switching Protocols`, `Upgrade: websocket`, `Connection: Upgrade`,
`Sec-WebSocket-Accept: {WS_ACCEPT}`; `C1000` is the frame `88 02 03 E8`.

**The upgrade request.** Every case sent, byte for byte apart from the random key
(`curl -sS ws://127.0.0.1:18301/chat`):

```
GET /chat HTTP/1.1\r\n
Host: 127.0.0.1:18301\r\n
User-Agent: curl/8.21.0\r\n
Accept: */*\r\n
Upgrade: websocket\r\n
Sec-WebSocket-Version: 13\r\n
Sec-WebSocket-Key: 2gOkvEj1E8ybsCpQqusccQ==\r\n
Connection: Upgrade\r\n
\r\n
```

No `Sec-WebSocket-Extensions` and no `Sec-WebSocket-Protocol` unless given with `-H` (then after
the key). `-u` adds `Authorization` right after `Host`; `--oauth2-bearer`, `--ntlm` and
`--aws-sigv4` likewise. `wss://` sends the same head inside TLS. `-d x=1` and `--http1.0` change
nothing: still `GET`, still `HTTP/1.1`, no body. `-I` sends `HEAD`.

| Case | Server's reply | curl arguments | Exit | stdout, stderr, and what curl sent after the head |
| --- | --- | --- | --- | --- |
| text then close | `H`, `81 05 hello`, `C1000` | `-sS -v` | 0 | stdout `hello` `03 E8`: curl writes a `CLOSE`'s payload too; `-v` notes `[WS] Received 101, switch to WebSocket`; curl sends nothing back |
| wrong accept | `H` with `Sec-WebSocket-Accept: AAAA...=`, `81 05 hello`, `C1000` | `-sS` | 0 | `hello` `03 E8`: **8.21.0 does not check the accept value** |
| no accept | `H` without `Sec-WebSocket-Accept` | `-sS` | 0 | the same |
| no `Upgrade` field | `H` without `Upgrade` | `-sS` | 0 | the same |
| `200` | `200 OK`, body `hello` | `-sS` | 22 | `curl: (22) Refused WebSocket upgrade: 200`; nothing written |
| `400`, `426`, `401`, `403`, `404`, `405` | each with `Content-Length` | `-sS` | 22 | `Refused WebSocket upgrade: <status>` |
| `301` | `Location: /other` | `-sS -L` | 22 | `Refused WebSocket upgrade: 301`: not followed |
| binary | `H`, `82 03 00 01 02`, `C1000` | `-sS` | 0 | `00 01 02 03 E8` |
| fragmented | `H`, `01 03 hel`, `00 01 l`, `80 01 o`, `C1000` | `-sS` | 0 | `hello` `03 E8` |
| two bursts | `H`, `81 01 a`; after a pause `81 01 b`, `C1000` | `-sS` | 0 | `ab` `03 E8` |
| 126-byte form | `81 7E 00 C8` and 200 bytes | `-sS` | 0 | all 200 bytes |
| 65536 bytes | `82 7F 00..01 00 00` and 65536 bytes | `-sS` | 0 | all 65536 bytes |
| 70000 bytes | one frame of 70000 bytes | `-sS` | 0 | all 70000 bytes |
| 131082 bytes | `02` 65536, `00` 65536, `80` 10 bytes, `88 00` | `-sS` | 0 | all 131082 bytes |
| empty binary | `82 00`, `88 00` | `-sS -w [%{http_code}]` | 0 | `[101]` only |
| text listing | `81 0B a.txt\nsub/\n`, `88 00` | `-sS` | 0 | `a.txt\nsub/\n` |
| `PING` | `H`, `89 04 ping`; after a pause `C1000` | `-sS -v` | 0 | `03 E8`; curl sent `8A 84` + mask + masked `ping`: **a masked `PONG` with the same payload** |
| `PING` of 125 | `89 7D` and 125 bytes | `-sS` | 0 | curl sent `8A FD` + mask + the 125 bytes masked |
| unsolicited `PONG` | `8A 02 hi`, `81 02 ok`, `C1000` | `-sS` | 0 | `hi` `ok` `03 E8`: **a `PONG`'s payload is written to stdout too** |
| close with reason | `81 02 hi`, `88 05 03 E9 bye` | `-sS` | 0 | `hi` `03 E9 bye` |
| close 1009, 1001 | `88 02 03 F1`, `88 02 03 E9` | `-sS` | 0 | `03 F1`, `03 E9` |
| empty close | `81 02 hi`, `88 00` | `-sS` | 0 | `hi` only |
| TCP close, no `CLOSE` | `81 02 hi`, then FIN | `-sS` | 0 | `hi` |
| close, then held open | `81 02 hi`, `C1000`, connection held 8 s | `-sS -m 4` | 28 | `Operation timed out after 4005 milliseconds with 0 bytes received`, stdout empty: **curl never answers a `CLOSE` and never closes; it waits for the server's TCP close** |
| held open | `81 02 hi`, held 6 s | `-sS -m 2` | 28 | the same, stdout empty |
| masked server frame | `81 85` + mask + 5 bytes | `-sS` | 56 | `[WS] masked input frame` |
| `RSV1` set | `C1 02 hi` | `-sS` | 56 | `[WS] invalid reserved bits: c1` |
| opcode 3 | `83 02 hi` | `-sS` | 56 | `[WS] invalid opcode: 83` |
| control over 125 | `89 7E 00 80` and 128 bytes | `-sS` | 56 | `[WS] received PING frame is too big` |
| fragmented control | `09 02 hi` | `-sS` | 56 | `[WS] invalid fragmented PING frame` |
| continuation first | `80 02 hi` | `-sS` | 56 | `[WS] no ongoing fragmented message to resume` |
| invalid UTF-8 text | `81 02 FF FE`, `C1000` | `-sS` | 0 | `FF FE 03 E8`: not checked |
| unrequested extension and subprotocol | `H` plus `Sec-WebSocket-Extensions: permessage-deflate` and `Sec-WebSocket-Protocol: chat`, `81 02 hi`, `88 00` | `-sS` | 0 | `hi` |
| `Date` and `Server` in the 101 | decision 2's head, `81 02 hi`, `88 00` | `-sS` | 0 | `hi` |
| `wss` | `H`, `81 02 hi`, `C1000`, over TLS | `-sS -k wss://.../secure` | 0 | `hi` `03 E8` |
| `-i` | `H`, `81 02 hi`, `C1000` | `-sS -i` | 0 | `hi` `03 E8`: **the `101` head is not written** |
| `-o`, `-w` | the same | `-sS -o out.bin -w "%{http_code} %{size_download}"` | 0 | stdout `101 8`, `out.bin` holds `hi` `03 E8` |
| `-I` | `405`, `Allow: GET` | `-sS -I` | 22 | `Refused WebSocket upgrade: 405`; with `H` instead, 52 `Empty reply from server` |
| `-u` Basic | `H`, `81 02 hi` | `-sS -u tester:secret` | 0 | `Authorization: Basic dGVzdGVyOnNlY3JldA==` sent unasked |
| `--oauth2-bearer` | the same | `-sS --oauth2-bearer tok` | 0 | `Authorization: Bearer tok` |
| `--aws-sigv4` | the same | `-sS --aws-sigv4 aws:amz:us-east-1:s3 -u AKID:SECRET` | 0 | `Authorization: AWS4-HMAC-SHA256 ...` and `X-Amz-Date` |
| `--digest` | `401` with a `Digest` challenge, then `H` | `-sS --digest -u tester:secret` | 22 | `Refused WebSocket upgrade: 401`: **curl does not answer a challenge** |
| `--anyauth` | `401` with `Basic`, then `H` | `-sS --anyauth -u tester:secret` | 22 | the same |
| `--ntlm` | `401` with an NTLM type 2 message | `-sS --ntlm -u tester:secret` | 22 | curl sent `Authorization: NTLM TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==` (type 1) unasked, then refused the `401` |
| `--negotiate` | `401` `Negotiate` | `-sS --negotiate -u :` | 22 | `InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS`: nothing sent (no KDC on the lane machine, ADR-0065) |

What this settles:

1. **curl sends nothing after the upgrade but `PONG`s.** It never sends a data frame and never
   sends or answers a `CLOSE`. After a `CLOSE` it waits for the server's TCP close, so a server
   that holds the connection hangs it until `-m` (28).
2. **curl writes every payload it receives** - data, `PONG` and `CLOSE` alike - to its output, in
   order, whole across frame boundaries. A `CLOSE` with a code puts two bytes of it in the
   downloaded file; an empty `CLOSE` puts nothing.
3. **curl checks framing, not the handshake.** It ignores `Sec-WebSocket-Accept`, `Upgrade`,
   unrequested extensions and subprotocols, and invalid UTF-8; it fails with 56 on a masked
   frame, reserved bits, a reserved opcode, a control frame over 125 bytes or fragmented, and a
   continuation with nothing to continue.
4. **Only credentials curl sends unasked can log in**: Basic, Bearer, AWS Signature Version 4.
   Any non-`101` status, `401` included, ends the transfer with 22.

**Not measured:** the Linux and macOS static builds (the lane runs Windows only); they are the
same tag's `lib/ws.c`, and BL-304 runs its cases on all three through CI. A server half-close
after its `CLOSE` (the recorder closes fully); BL-302's tests and BL-304 prove decision 5's lingering close.

## Decision

### 1. Which request is upgraded, and each refusal

Each check runs in this order; the first that fails answers. Every refusal is
`HTTP/1.1 <status> <reason>`, `Date`, `Server: surl` (ADR-0019 decision 1), the fields named below,
`Content-Length: 0`, and `Connection: close`, with an empty body, and the connection is then closed
as the HTTP server closes a refusal (ADR-0019, with ADR-0024's drain before the close) - except `401`, which keeps the connection as ADR-0032 section 4
says, its next request being read as a new upgrade request.

| # | Check (RFC 6455 section 4.2.1) | Refusal |
| --- | --- | --- |
| 1 | The head is read within `--head-timeout` and `--max-request-head`, and parses (ADR-0070's reader) | `408`, `431`, `400`, exactly as the HTTP server answers them (ADR-0019) |
| 2 | Exactly one `Host` field | `400 Bad Request` |
| 3 | The login (decision 3) | `401 Unauthorized` with ADR-0032's challenges, or `403 Forbidden` |
| 4 | The method is `GET` | `405 Method Not Allowed`, `Allow: GET` |
| 5 | The version is `HTTP/1.1` | `400 Bad Request` (`HTTP/1.0`; any other version is the reader's `505`) |
| 6 | `Upgrade` lists the token `websocket` (case-insensitive) | `426 Upgrade Required`, `Upgrade: websocket`, `Sec-WebSocket-Version: 13` (RFC 9110 section 15.5.22 needs the `Upgrade` field) |
| 7 | `Connection` lists the token `Upgrade` (case-insensitive) | `400 Bad Request` |
| 8 | Exactly one `Sec-WebSocket-Key`, base64 that decodes to exactly 16 bytes | `400 Bad Request` |
| 9 | Exactly one `Sec-WebSocket-Version`, and it is `13` | `426 Upgrade Required`, `Sec-WebSocket-Version: 13` (RFC 6455 section 4.4); a missing field is a version surl does not speak |
| 10 | The request path names a file, or a directory with `--list-directories`, in the content store, after ADR-0006 section 2's exposure checks (not under `--ws-echo`, decision 4) | `404 Not Found` |

Checks 2 to 9 look only at the fields; a body announced by `Content-Length` or `Transfer-Encoding`
is refused `400` at check 7's position, since an upgrade `GET` carries none and its bytes would be
read as frames.

**Why this order.** The login comes before the method and the WebSocket fields for the reason
ADR-0032 section 4 gives for HTTP: a client that may not connect learns nothing about the server.
**Why `426` for the version but `400` for the other fields:** RFC 6455 section 4.4 names `426` with
`Sec-WebSocket-Version` for a version the server does not speak, and RFC 9110's `426` is for a
request that did not ask to upgrade at all - a plain HTTP client at a `ws://` listener - which is
check 6. Every refusal is exit 22 to curl (measured: any non-`101`).

### 2. The `101`

```
HTTP/1.1 101 Switching Protocols\r\n
Date: <IMF-fixdate>\r\n
Server: surl\r\n
Upgrade: websocket\r\n
Connection: Upgrade\r\n
Sec-WebSocket-Accept: <base64 of SHA-1 of the key and 258EAFA5-E914-47DA-95CA-C5AB0DC85B11>\r\n
\r\n
```

`Date` and `Server` as ADR-0019 has on every response (measured: curl accepts them). The accept
value is BL-288's, correct though 8.21.0 does not check it: other clients do, and RFC 6455
requires it.

**Subprotocols and extensions: none.** A `Sec-WebSocket-Protocol` or `Sec-WebSocket-Extensions`
the client sends is ignored, and the `101` names neither (RFC 6455 sections 4.2.2 and 9.1: a server
that selects none sends none). curl offers neither unasked, and `permessage-deflate` would need
compression state the tool never asks for.

### 3. Logins

The upgrade request is judged through `IHttpAuthenticationSession` exactly as the HTTP server
judges a `GET` (ADR-0032 sections 4 and 6), because it is one:

- **Anonymous upgrade is a read.** With no account configured it needs no login, like an HTTP
  `GET`; once any account is configured every upgrade needs one; `--allow-anonymous` serves every
  upgrade as anonymous. Nothing an upgrade does writes to the content store.
- A plain-text secret (Basic, Bearer) on `ws://` without `--allow-plaintext-auth` is `403`
  unchecked; on `wss://` it is checked.
- The `401` carries ADR-0032 section 4's challenges, one `WWW-Authenticate` field per value the
  session gives.

**What upstream curl completes.** Measured, curl 8.21.0 refuses every `401` (22), so only the
methods it sends unasked log in: Basic (`-u`), Bearer (`--oauth2-bearer`) and AWS Signature
Version 4 (`--aws-sigv4`). Digest, NTLM and Negotiate are still offered and checked, as the HTTP
server does, for clients that answer a challenge; curl's `--digest`, `--ntlm`, `--negotiate` and
`--anyauth` end 22 at the `401`, and BL-304 pins that. **Rejected:** answering those methods'
first leg with a `101` so curl's tool could "log in" - it would serve a client that proved nothing.

### 4. What surl sends once upgraded

**By default, the content at the request path, then an empty `CLOSE`.** The `curl` tool upgrades
and writes what it receives, so `curl ws://host/file -o file` is a download:

- **A file** is sent as one **binary** message: frames of at most **65536** payload bytes, the first
  opcode `2`, the rest continuation (`0`), `FIN` on the last; an empty file is one `82 00` frame.
  Binary because a file is bytes and a text frame must be valid UTF-8 (RFC 6455 section 5.6);
  curl writes both alike (measured). 65536 is the largest frame libcurl documents delivering whole
  (`WEBSOCKET.md`) and bounds what surl buffers; measured, curl writes a 131082-byte message sent
  that way byte for byte.
- **A directory**, only with `--list-directories` (ADR-0006 section 2), is one **text** message:
  each entry of `ContentDirectoryListing.Entries`, in its ordinal order, its name then `/` for a
  directory, each followed by LF, in UTF-8 (measured: written as sent). An empty directory is an
  empty text message.
- Then **`CLOSE` with no payload** (`88 00`), because curl writes a `CLOSE`'s code into the
  downloaded file (measured), and an empty `CLOSE` is RFC 6455's normal end (status 1005 on the
  client side). Then decision 5's close.
- Client frames arriving meanwhile are read between surl's frames and answered as decision 6 says;
  a client `CLOSE` stops the message after the current frame.

**With `--ws-echo`, an echo server.** A new option (category `websocket`, no argument, not
`testing`: it widens nothing): the request path is not looked up (check 10 is skipped), surl sends
nothing of its own, and every complete client data message is sent back whole as one message with
its opcode (fragmented as above), until the client's `CLOSE`, a protocol error or a limit. It is
how a libcurl program driving `curl_ws_send` and `curl_ws_recv` gets answers, and how BL-304 proves
the frames only libcurl sends (decision 10). **Rejected:** echo as the default (the tool would wait
for the idle timeout and download nothing); both at once, file then echo (the tool would hang after
the file until the idle timeout, measured 28 under `-m`); a magic path for echo (a content store
may hold any path).

surl never sends `PING`: RFC 6455 makes it optional, the idle timeout (decision 7) ends a silent
peer, and curl writes every payload it receives, so a `PING`'s payload would land in the
downloaded file (measured: the unsolicited `PONG`'s payload did). So BL-304 cannot see curl's
`PONG` against surl; the measured `PONG` bytes are BL-302's fixture for decision 6's "`PONG`
ignored".

### 5. How surl closes

After surl sends a `CLOSE` - its own, or its echo of the client's - it shuts down its sending side
and reads and discards what the client still sends until the client closes or **one second**
passes (ADR-0059's farewell window), then closes the connection. It never waits for a `CLOSE`
answer: curl never sends one (measured), and a server that held the connection would hang curl
until `-m`. RFC 6455 section 7.1.1 asks the server to close the TCP connection first; the lingering
read keeps unread client bytes from turning the close into a reset that could cost curl the
message's end.

### 6. How every client frame is answered

A client frame is judged in this order; a failure sends `CLOSE` with the code (two bytes, no
reason), then decision 5's close. The codes are RFC 6455 section 7.4.1's.

| Client frame | Answer |
| --- | --- |
| Not masked (`MASK` 0) | close **1002** (RFC 6455 section 5.1) |
| `RSV1`, `RSV2` or `RSV3` set (no extension was agreed) | close **1002** |
| Opcode `3` to `7` or `B` to `F` | close **1002** |
| A control frame (`8`, `9`, `A`) with payload over 125 bytes, or without `FIN` | close **1002** |
| Continuation (`0`) with no message in progress, or a data frame (`1`, `2`) while one is | close **1002** |
| A payload length past `--max-message` (checked from the header, before the payload is read), or a reassembled message past it | close **1009** |
| A 64-bit length with the top bit set, or a length not in its shortest form | close **1002** |
| `PING` | `PONG` with the same payload, sent before surl's next data frame (RFC 6455 section 5.5.3) |
| `PONG` | ignored |
| `CLOSE` with no payload | `CLOSE` with no payload, then decision 5 |
| `CLOSE` with a code 1000 to 1003, 1007 to 1014, or 3000 to 4999, and a reason that is valid UTF-8 (1012 to 1014: Amendment 2) | `CLOSE` echoing the code (two bytes, no reason), then decision 5 |
| `CLOSE` with a 1-byte payload, any other code (1004, 1005, 1006, below 1000, 1015 to 2999 unregistered, 5000 and above), or invalid UTF-8 in its reason | close **1002** (or **1007** for the reason's UTF-8) |
| A text message (echoed or not) that is not valid UTF-8 once reassembled | close **1007** |
| A data message without `--ws-echo` | read, checked as above, and discarded |

The `PONG` and every surl frame are unmasked (RFC 6455 section 5.1; curl fails on a masked one,
measured). Fragments are reassembled by BL-288's reassembler; interleaved control frames are
answered in place.

### 7. Limits (ADR-0006)

| Limit | WebSocket behaviour |
| --- | --- |
| `--max-request-head`, `--head-timeout` | the upgrade head, through ADR-0070's reader: `431`, `408` (decision 1, check 1) |
| `--max-message` (1 MiB) | one client frame's payload, and a reassembled client message: close **1009** before the excess is read. surl's own frames are 65536 bytes whatever it is |
| `--max-filesize` | not applied: surl stores nothing a WebSocket client sends |
| `--idle-timeout` (120 s) | no byte either way: surl sends `CLOSE` **1001** ("going away") then decision 5, on ADR-0059's own one-second deadline. A client that stops reading is idle too; a `CLOSE` that cannot be written in the second is dropped with the connection |
| `--max-time` (3600 s) | the same close **1001** |
| `--max-connections`, `--max-connections-per-address` | the engine's, before any byte, as for every server |
| Shutdown | no farewell (ADR-0059 decision 4): the connection is closed |

**Why 1001 and not 1008 for the timeouts:** 1008 is a message that violates policy; RFC 6455's
1001 is "an endpoint going away, such as a server going down", which is what the peer sees.

### 8. `wss`

`wss` is TLS from the first byte through `ImplicitTlsSchemeServer` (ADR-0010), ALPN `http/1.1`,
the certificate from `--cert` or `--self-signed`; after the handshake the exchange is `ws`'s byte
for byte (measured: curl's head inside TLS is the same). `ws://` and `wss://` are the only listen
URLs that answer an upgrade (ADR-0070).

### 9. Verbose notes and help

Notes are `IExchangeLog.Note` at `verbose` and above (ADR-0033 section 3), peer bytes escaped as
ADR-0006 section 3 says; frame bytes are in the engine's `--trace` dumps, with no extra note per
frame.

| When | Note |
| --- | --- |
| A refusal | `WebSocket upgrade refused: <status> <reason>: <check>`, e.g. `WebSocket upgrade refused: 426 Upgrade Required: Sec-WebSocket-Version is not 13` |
| The `101` | `WebSocket upgrade accepted for <path>` (or `... for <path>, echoing` under `--ws-echo`) |
| A file or listing sent | `Sent <n> bytes of <path> as a <binary or text> message in <k> frames` |
| A `PING` answered | `Answered PING with PONG, <n> bytes` |
| A client `CLOSE` | `Client closed with <code or "no code">` |
| surl's `CLOSE` with a code | `Closing with <code>: <why>`, e.g. `Closing with 1009: a frame of 2097152 bytes is past --max-message 1048576` |

**Help.** The category `websocket`, `WebSocket protocol` (ADR-0034 decision 1's wording, as `ssh`
names the protocol rather than its schemes: curl 8.21.0 has no WebSocket category to copy), claims
the schemes `ws` and `wss`, and holds every option the server reads: `--max-request-head`,
`--head-timeout`, `--max-message`, `--user`, `--user-file`, `--allow-anonymous`,
`--allow-plaintext-auth`, `--auth`, `--directory`, `--list-directories`, and `--ws-echo`
(`Echo client messages, not the path`). That description is kept under 39 characters because
`HelpLayout` (ADR-0034 decision 3, curl's measured column rule) narrows the description column of
`surl --help all` for every option once one description reaches 39, so a longer one would move the
whole list's column from 38. The `--aihelp` topic is `websocket`.
BL-303 adds the category, the option, the topic's prose and example, and grows the pinned topic
lists, as root `CLAUDE.md` requires.

### 10. libcurl: pin `libcurl-4.dll`

**Decided: pin it.** The frames only libcurl's API sends (client text, binary, fragmented, `PING`,
`CLOSE` with a code) are part of "every request upstream curl can make", and surl's answers to them
(decision 6) should be proved from upstream rather than rest on RFC 6455 alone. The library that
the reference `curl.exe` runs is already on disk beside it - `C:\Program
Files\Git\mingw64\bin\libcurl-4.dll`, 898732 bytes, product version 8.21.0, SHA-256
`799F7EEFC3C9DA9C80EC5AEA221A02B3AFE2C5350C6B45FD5A4865E7E2D4E574` (read 2026-09-30) - so pinning
it downloads nothing and needs no question for Stewart. A C# file-based app (`dotnet run`, P/Invoke
into the pinned file only, BCL only) drives `curl_easy_*` with `CURLOPT_CONNECT_ONLY` 2 and
`curl_ws_send`/`curl_ws_recv`.

- **BL-323** (feature) pins it: an entry of kind library in `UpstreamCurlBuilds.json` that
  `Surl.Conformance.UnitLibrary`'s `UpstreamCurlBuildPins` and `UpstreamCurlLocator` and
  `Record-CurlExchange.ps1`'s pin check know, and the driver.
- **BL-322** (docs) measures the client frames through it against the recorder and amends this ADR
  with them.
- BL-304 depends on both. Until they land, decision 6 rests on RFC 6455, which dictates it.
- **Linux and macOS:** the static builds carry no shared library, so the libcurl cases run on
  Windows only and report `Inconclusive` elsewhere; the tool cases run on all three.

**Rejected:** not pinning, and proving decision 6 only with hand-written frames: those would be
Surl's own reading of the RFC checked against itself, which ADR-0003 exists to avoid.

### 11. What BL-304 proves with the pinned build

Against `surl` over loopback through the conformance harness; `P` is a temporary `--directory`
holding `file.bin` (200000 bytes, so the message spans four frames), `empty.bin` (0 bytes) and
`sub/a.txt`; `-k` wherever `--self-signed` is used.

| surl options | curl command line | Exit, and what is checked |
| --- | --- | --- |
| `-d P` | `curl -sS ws://127.0.0.1:<p>/file.bin` | 0, stdout is `file.bin` byte for byte |
| `-d P` | `curl -sS -o out.bin ws://.../file.bin` | 0, `out.bin` is `file.bin` |
| `-d P` | `curl -sS -w "%{http_code}" ws://.../empty.bin` | 0, stdout `101` |
| `-d P --self-signed` | `curl -sS -k wss://.../file.bin` | 0, stdout is `file.bin` |
| `-d P` | `curl -sS ws://.../missing` | 22, `Refused WebSocket upgrade: 404` |
| `-d P` | `curl -sS ws://.../sub/` | 22 (`404`: no `--list-directories`) |
| `-d P --list-directories` | `curl -sS ws://.../sub/` | 0, stdout `a.txt\n` |
| `-d P` | `curl -sS -I ws://.../file.bin` | 22 (`405`) |
| `-d P --user tester:secret` | `curl -sS ws://.../file.bin` | 22 (`401`) |
| `-d P --user tester:secret` | `curl -sS -u tester:secret ws://.../file.bin` | 22 (`403`: Basic in plain text) |
| `-d P --user tester:secret --allow-plaintext-auth` | the same | 0, `file.bin` |
| `-d P --user tester:secret --self-signed` | `curl -sS -k -u tester:secret wss://.../file.bin` | 0, `file.bin` |
| `-d P --user tester:secret --self-signed` | `curl -sS -k -u tester:wrong wss://.../file.bin` | 22 (`401`) |
| `-d P --user :tok --self-signed` | `curl -sS -k --oauth2-bearer tok wss://.../file.bin` | 0, `file.bin` |
| `-d P --user AKID:SECRET --self-signed` | `curl -sS -k --aws-sigv4 aws:amz:us-east-1:s3 -u AKID:SECRET wss://.../file.bin` | 0, `file.bin` |
| `-d P --user tester:secret` | `curl -sS --digest -u tester:secret ws://.../file.bin` | 22 (curl refuses the `401`) |
| `-d P --user tester:secret --auth basic,ntlm --allow-plaintext-auth` | `curl -sS --ntlm -u tester:secret ws://.../file.bin` | 22 (the type 2 `401`) |
| `--ws-echo --idle-timeout 2` | `curl -sS ws://.../` | 0 after about 2 s, stdout `03 E9` (surl's close 1001) |
| `--ws-echo --max-time 2` | `curl -sS ws://.../` | 0, stdout `03 E9` |
| `--ws-echo --idle-timeout 30` | `curl -sS -m 2 ws://.../` | 28 |

Plus, once BL-323 and BL-322 land, through the pinned `libcurl-4.dll` against `--ws-echo` (Windows
only): a text, a binary and a three-fragment message each received back whole; a `PING` answered
by a `PONG` with its payload; `CLOSE` 1000 answered by `CLOSE` 1000 and the close; a 2 MiB message
answered by `CLOSE` 1009; a text message of `FF FE` answered by `CLOSE` 1007 - with the
`curl_ws_recv` results BL-322 records.

The expectations are pinned from this ADR's measurements; where the Linux or macOS build gives
another exit, it is pinned per platform in its own test (root `CLAUDE.md`) and recorded against
this ADR.

## Alternatives considered

- **Check nothing and upgrade any `GET`**, as curl checks nothing of surl's `101`. Rejected: other
  clients send defective upgrades, and RFC 6455 section 4.2.1 lists what a server must require.
- **A `CLOSE` 1000 at the end of a file.** Rejected: curl writes `03 E8` into the downloaded file
  (measured); the empty `CLOSE` is equally normal.
- **Send a file as a single frame.** Rejected: a 1 GiB frame is 1 GiB buffered on both sides and
  past most clients' limits; 65536-byte frames cost curl nothing (measured).
- **Text for text files.** Rejected: surl would have to validate every file's UTF-8 and guess its
  type, and curl writes both opcodes the same.
- **Wait for the client's `CLOSE` answer.** Rejected: curl never sends one (measured).

## Consequences

- BL-301 builds decisions 1 to 3 and 9's notes; BL-302 decisions 4 to 7; BL-303 decision 9's help
  and `--ws-echo`'s registration with decision 8's `wss`; BL-304 decision 11.
- `--ws-echo` is a new option, so BL-303's `--aihelp` facts, categories and example cover it.
- `Record-CurlExchange.ps1 -Raw` answers a WebSocket upgrade through `{WS_ACCEPT}`, so BL-301's
  fixtures (the recorded upgrade requests) and BL-302's (the masked `PONG`) are recorded with it.
- BL-323 and BL-322 are filed and BL-304 depends on them.
- The server-half-close in decision 5 was not measured; if BL-304 finds curl mishandles it, the
  finding amends decision 5.

## Amendment 1 - libcurl's client frames, measured (BL-322, 2026-09-30)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30.

Measured 2026-09-30 on Windows with the pinned `C:\Program Files\Git\mingw64\bin\libcurl-4.dll`,
SHA-256 `799F7EEFC3C9DA9C80EC5AEA221A02B3AFE2C5350C6B45FD5A4865E7E2D4E574` (kind library in
`UpstreamCurlBuilds.json`), driven by BL-323's `Run-LibcurlWebSocketScript.cs` through
`Record-CurlExchange.ps1 -Raw -RawIdleMilliseconds 400 -LibcurlWebSocket`. Every case's first
reply was `H` (as above); its second reply, sent once the driver's frames paused, is the
"Server's frame" column. A command was, for the text case:

```
.\Record-CurlExchange.ps1 -Port 18923 -Raw -RawIdleMilliseconds 400 -LibcurlWebSocket -RawReply 'HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: {WS_ACCEPT}\r\n\r\n','\x81\x05hello' -CurlArgs 'ws://127.0.0.1:18923/chat','send:TEXT:hello','recv' -OutDirectory <dir>
```

The other cases change only `-RawReply`'s second value and the steps. To send 2 MiB, which no
command line holds, this task gave the driver the step `send*<count>:<FLAGS>:<payload>` (the
payload repeated) and made a send that libcurl takes in part go on from where it stopped, as
`curl_ws_send`'s documentation requires. `M` below is the four-byte mask, which libcurl draws
at random for every frame; the payload after it is masked with it.

**The upgrade request** is the tool's head without `User-Agent` (the driver sets no
`CURLOPT_USERAGENT`): `GET /chat HTTP/1.1`, `Host`, `Accept: */*`, `Upgrade: websocket`,
`Sec-WebSocket-Version: 13`, `Sec-WebSocket-Key`, `Connection: Upgrade`.

| Case | Driver steps | Bytes libcurl sent after the head | Server's frame | What the driver printed |
| --- | --- | --- | --- | --- |
| text | `send:TEXT:hello`, `recv` | `81 85` `M` + 5 | `81 05 hello` | send `CURLcode 0`, sent 5; recv `CURLcode 0`, flags `TEXT`, offset 0, bytesleft 0, `hello` |
| binary | `send:BINARY:\x00\x01\x02`, `recv` | `82 83` `M` + 3 | `82 03 00 01 02` | recv flags `BINARY`, 3 bytes `00 01 02` |
| three fragments | `send:TEXT+CONT:hel`, `send:TEXT+CONT:l`, `send:TEXT:o`, `recv` | `01 83` `M` + 3, `00 81` `M` + 1, `80 81` `M` + 1 | `81 05 hello` | each send `CURLcode 0`; recv flags `TEXT`, 5 bytes `hello` |
| `PING` | `send:PING:ping`, `recv` | `89 84` `M` + 4 | `8A 04 ping` | recv flags `PONG`, 4 bytes `ping`: **a `PONG` reaches the application** |
| `CLOSE` 1000 | `send:CLOSE:\x03\xE8`, `recv`, `recv` | `88 82` `M` + `03 E8` | `88 02 03 E8`, then the TCP close | recv flags `CLOSE`, 2 bytes `03 E8`; recv `CURLcode 52` |
| `CLOSE` with a reason | `send:CLOSE:\x03\xE8bye` | `88 85` `M` + `03 E8 bye` | - | sent 5 |
| client `PONG` | `send:PONG:hi` | `8A 82` `M` + `hi` | - | sent 2 |
| length forms | `send*125:TEXT:t`, `send*126:TEXT:u`, `send*65535:BINARY:v`, `send*65536:BINARY:w` | `81 FD`, `81 FE 00 7E`, `82 FE FF FF`, `82 FF 00 00 00 00 00 01 00 00`, each + `M` + payload | - | each sent whole in one call |
| 2 MiB | `--recv-timeout 60000`, `send*2097152:BINARY:a`, `recv`, `recv` | `82 FF 00 00 00 00 00 20 00 00` `M` + 2097152 | `88 02 03 F1`, then the TCP close | send `CURLcode 0`, sent 2097152 in 8 calls; recv flags `CLOSE`, 2 bytes `03 F1`; recv `CURLcode 52` |
| text `FF FE` | `send:TEXT:\xFF\xFE`, `recv`, `recv` | `81 82` `M` + `FF FE`: **libcurl sends invalid UTF-8 unchecked** | `88 02 03 EF`, then the TCP close | recv flags `CLOSE`, 2 bytes `03 EF`; recv `CURLcode 52` |
| server `CLOSE` 1009 | `send:TEXT:hello`, `recv`, `recv` | `81 85` `M` + 5 | `88 02 03 F1`, then the TCP close | recv flags `CLOSE`, 2 bytes `03 F1`; recv `CURLcode 52` |
| empty server `CLOSE` | the length-form steps, `recv` | as above | `88 00` | recv flags `CLOSE`, 0 bytes |
| server `CLOSE`, connection held 4 s | `send:TEXT:x`, `recv` | `81 81` `M` + `x`, and nothing after the `CLOSE` | `88 02 03 E8` | recv flags `CLOSE`, `03 E8`; at `curl_easy_cleanup` libcurl closed the TCP connection **without sending a `CLOSE`** |
| unsolicited `PONG` | `send:TEXT:x`, `recv`, `recv` | `81 81` `M` + `x` | `8A 02 hi`, `81 02 ok` | recv flags `PONG`, `hi`; recv flags `TEXT`, `ok` |
| server `PING` | `send:TEXT:x`, `recv`, `send:TEXT:y`, `recv` | `81 81` `M` + `x`; then `8A 84` `M` + `ping` and `81 81` `M` + `y` in one write | `89 04 ping`, `81 02 ok` | recv flags `TEXT`, `ok` (the `PING` is not returned): **the automatic `PONG` waits for the application's next `curl_ws_send`** |
| a message in 65536-byte frames | `send:TEXT:x`, five `recv` | `81 81` `M` + `x` | `01 7F ..01 00 00` + 65536, `00 7F ..01 00 00` + 65536, `80 0A` + 10 | recv flags `TEXT+CONT`, 65536 bytes; recv flags `TEXT+CONT`, 65536 bytes; recv flags `TEXT`, 10 bytes; then `CURLcode 52` |
| one 70000-byte frame | `send:TEXT:x`, `recv`, `recv` | as above | `82 7F ..01 11 70` + 70000 | recv flags `BINARY`, offset 0, bytesleft 4464, 65536 bytes; recv flags `BINARY`, offset 65536, bytesleft 0, 4464 bytes |

What this settles:

1. **libcurl's frames are what decision 6 assumes**: every frame masked with a fresh key, `RSV`
   clear, the length in its shortest form, `FIN` clear on every fragment but the last, the
   first fragment carrying the opcode and the rest `0`. Nothing measured contradicts decision 6,
   so it stands as written.
2. **libcurl never answers a `CLOSE` itself** through the API, and its automatic `PONG` is sent
   only with the application's next `curl_ws_send`. Decision 5 (surl never waits for a `CLOSE`
   answer) is right for libcurl as for the tool; decision 4's "surl never sends `PING`" also
   spares a libcurl program a `PONG` that may never come.
3. **libcurl does not check UTF-8 on the way out**, so BL-304's `FF FE` case reaches surl and
   decision 6's 1007 is what answers it.
4. **`curl_ws_recv` reports a continuation frame with the message's type plus `CONT`**, and a
   frame larger than the buffer in pieces through `offset` and `bytesleft`; decision 4's
   65536-byte frames therefore arrive one `curl_ws_recv` each into a 65536-byte buffer.

**What BL-304 expects through the pinned `libcurl-4.dll` against `surl --ws-echo`** (decision 11's
libcurl rows; Windows only, `Inconclusive` elsewhere). Each starts `perform: CURLcode 0`.

| Case (decision 11) | Driver steps | Expected |
| --- | --- | --- |
| text | `send:TEXT:hello`, `recv` | recv `CURLcode 0`, flags `TEXT`, offset 0, bytesleft 0, 5 bytes `hello` |
| binary | `send:BINARY:\x00\x01\x02`, `recv` | recv flags `BINARY`, 3 bytes `00 01 02` |
| three-fragment message | `send:TEXT+CONT:hel`, `send:TEXT+CONT:l`, `send:TEXT:o`, `recv` | recv flags `TEXT`, 5 bytes `hello` (surl echoes the message as one frame, decision 4) |
| `PING` | `send:PING:ping`, `recv` | recv flags `PONG`, 4 bytes `ping` |
| `CLOSE` 1000 | `send:CLOSE:\x03\xE8`, `recv`, `recv` | recv flags `CLOSE`, 2 bytes `03 E8`; then recv `CURLcode 52` once surl closes (decision 5) |
| 2 MiB message | `send*2097152:BINARY:a`, `recv`, `recv` | send `CURLcode 0`, sent 2097152 (surl reads and discards the rest during decision 5's one-second linger); recv flags `CLOSE`, 2 bytes `03 F1`; then `CURLcode 52` |
| text `FF FE` | `send:TEXT:\xFF\xFE`, `recv`, `recv` | recv flags `CLOSE`, 2 bytes `03 EF`; then `CURLcode 52` |

**Not measured:** surl's own early close of the 2 MiB case before the payload is read (the
recorder replies only once the driver pauses); if libcurl's send fails with a reset there, BL-304
pins the measured `CURLcode` against this amendment. The Linux and macOS builds carry no shared
libcurl (decision 10).

## Amendment 2 - close codes 1012 to 1014 are echoed (BL-340, 2026-10-01)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-10-01, in
BL-340, from BL-305's finding.

Decision 6 as first written answered a client `CLOSE` carrying 1012 to 2999 with close **1002**.
The code does not, and the code is right: `WebSocketCloseCodes.IsAllowedOnTheWire` (BL-288)
allows 1000 to 1003, 1007 to 1014 and 3000 to 4999. The IANA WebSocket Close Code Number
Registry, which RFC 6455 section 7.4.2 reserves 1000 to 2999 for, has since assigned 1012
(Service Restart), 1013 (Try Again Later) and 1014 (Bad Gateway), so a peer may send them and
the protocol's current specification is the registry, not the RFC's 2011 list.

So a client `CLOSE` with 1012, 1013 or 1014 and a reason that is valid UTF-8 is echoed like
1000, then decision 5; a client `CLOSE` with 1015 to 2999 (1015 must never be sent, the rest is
unassigned) is answered close **1002**. Decision 6's table says so above. The Product overview
and the Glossary already state this set (BL-305); no code changes.
