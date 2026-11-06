# ADR-0088 — How Surl serves the HTTP `CONNECT` proxy and the HTTPS proxy

- **Status:** Accepted
- **Date:** 2026-11-04; Amendment 1 (BL-552) 2026-11-05; Amendment 2 (BL-554) 2026-11-05; Amendment 3 (BL-553) 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-550. Built by BL-551 (decision 2's contract), BL-552 (decisions 1, 3, 4 and the limits of
  decision 7), BL-553 (decision 5), BL-554 (decision 6), BL-555 (decision 7's command line) and
  BL-556 (decision 8). Follow-ups filed: BL-594 (`ftp://` forwarding, decision 3) and BL-595 (HTTP/2
  to the HTTPS proxy, decision 6).
- **Amends:** [ADR-0002](ADR-0002-mirror-the-curl-ports-project-map.md) decision 3: its table gains
  decision 1's row. [ADR-0070](ADR-0070-the-http-message-library-the-http-websocket-and-rtsp-servers-share.md)
  decision 1: `Surl.Protocol.HttpProxy` is a fourth user of `Surl.HttpMessage`.
  [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) section 1: `--max-request-head` is also
  enforced by `Surl.Protocol.HttpProxy` (decision 7).

## Context

The product overview puts "Proxies" in scope: acting as the HTTP `CONNECT` proxy and the HTTPS proxy
that curl's `-x`/`--proxy`, `-p`/`--proxytunnel`, `-U`/`--proxy-user`, `--proxy-anyauth`,
`--proxy-basic`, `--proxy-digest`, `--proxy-ntlm`, `--proxy-negotiate`, `--proxy-cacert`,
`--proxy-insecure` and `--proxy-http2` talk to (curl 8.21.0's manpage,
https://curl.se/docs/manpage.html). ADR-0080 decision 2 marks the upstream cases that need one
`server-not-built`. Classified by BL-399 at tag `curl-8_21_0` (commit
`68720b4837284335b2d63cb358f8f6ce65f5bc55`), 29 cases wait on this ADR:

- `http-proxy` (24): 80, 83, 95, 275, 445, 714, 715, 744, 1078, 1230, 1287, 1288, 1297, 1319, 1320,
  1321, 1428, 1849, 1904, 2050, 2055, 2107, 3028, 3106;
- `https-proxy` (5): 1586, 1630, 1631, 1632, 3401;
- 715 and 2055 also need `socks5` (BL-557's family), so they pass only once both exist.

### What upstream curl 8.21.0 sends to a proxy

Measured by BL-550 with `Record-CurlExchange.ps1` against the Windows reference build (Git for
Windows' mingw64 `curl.exe`, SHA-256 `0E773709…8778`, `UpstreamCurlBuilds.json`), each run with
`-sS`, the proxy at `127.0.0.1` and the origin named `origin.test`, which curl never resolves when it
uses a proxy:

| # | curl arguments (after `-sS -x http://127.0.0.1:<port>`) | Proxy's canned answer | Exit | What curl sent |
| --- | --- | --- | --- | --- |
| 1 | `http://origin.test/a?b` | `200`, `ok` | 0 | `GET http://origin.test/a?b HTTP/1.1`, `Host: origin.test`, `User-Agent: curl/8.21.0`, `Accept: */*`, `Proxy-Connection: Keep-Alive` |
| 2 | `-p http://origin.test:8080/a` | `HTTP/1.1 200 Connection established\r\n\r\n`, then `200 ok` on the same connection | 0 | `CONNECT origin.test:8080 HTTP/1.1`, `Host: origin.test:8080`, `User-Agent`, `Proxy-Connection: Keep-Alive` (no `Accept`); then, inside the tunnel, `GET /a HTTP/1.1` with `Host: origin.test:8080` and no `Proxy-Connection` |
| 3 | `-p http://origin.test/a` | `HTTP/1.0 200 Connection established` with `Content-Length: 5`, then `200 ok` | 0 | `CONNECT origin.test:80`; curl ignored the `Content-Length` and the version, as RFC 9110 section 9.3.6 requires |
| 4 | `-p http://origin.test:25/a` | `403` | 7 | `CONNECT origin.test:25`; stderr `curl: (7) CONNECT tunnel failed, response 403` |
| 5 | `-p http://origin.test:80/a` | `502` | 7 | `CONNECT`; `curl: (7) CONNECT tunnel failed, response 502` |
| 6 | `-p https://origin.test/a` | `407` with `Proxy-Authenticate: Basic realm="surl"` | 7 | `CONNECT origin.test:443`; `curl: (7) CONNECT tunnel failed, response 407` (no credentials) |
| 7 | `-p --proxy-anyauth -U u:p http://origin.test/a` | `407` Basic, then the tunnel's `200`, then `200 ok` | 0 | `CONNECT` without credentials, then a second `CONNECT` on the **same connection** with `Proxy-Authorization: Basic dTpw`, then the tunnelled `GET` |
| 8 | `-U u:p http://origin.test/a` | `200` | 0 | `Proxy-Authorization: Basic dTpw` unasked, between `Host` and `User-Agent`: Basic is curl's default proxy method |
| 9 | `--proxy-anyauth -U u:p http://origin.test/a` | `407` with `Proxy-Authenticate: Digest realm="surl", nonce="abc", qop="auth"`, then `200` | 0 | the request without credentials, then on the same connection `Proxy-Authorization: Digest username="u",realm="surl",nonce="abc",uri="/a",cnonce="…",nc=00000001,response="…",qop="auth"`: **the Digest `uri` is the target's path, not the absolute-form** |
| 10 | `-m 3 --proxy-ntlm -U u:p http://origin.test/a` | `200` | 0 | `Proxy-Authorization: NTLM TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==` unasked (a NEGOTIATE_MESSAGE) |
| 11 | `http://origin.test/a` | `502` with body `bad` | 0 | the forwarded `GET`; curl prints the proxy's body and exits 0 like any other status |
| 12 | `-m 3 ftp://origin.test/a.txt` | `200` | 0 | `GET ftp://origin.test/a.txt HTTP/1.1`, `Host: origin.test:21`, `Proxy-Connection: Keep-Alive` |
| 13 | `-x https://127.0.0.1:<port> --proxy-insecure http://origin.test/a` (`-Tls`) | `200` | 0 | TLS to the proxy, then inside it exactly row 1's bytes |

Every request ends `\r\n\r\n`; no request above carried a body. A 407 reply whose `Proxy-Authenticate`
was a bare `NTLM`, sent in answer to row 10's NEGOTIATE_MESSAGE, left curl waiting past the
recorder's run; BL-553 measures NTLM's three legs whole.

## Decision

### 1. The library

| Horizontal library or server | May itself reference | Holds |
| --- | --- | --- |
| `Surl.Protocol.HttpProxy.UnitLibrary`, in `Protocols/Http/` | `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.HttpMessage.UnitLibrary`, `Surl.Authentication.UnitLibrary` | the HTTP and HTTPS proxy server, decisions 3 to 6 |

- The planning assumption is adopted, with its `.UnitTests` twin beside it. Namespace
  `Surl.Protocol.HttpProxy`. It is a protocol server: it references no other protocol server
  (not `Surl.Protocol.Http`), constructs no socket, and is held to the solution's quality gates.
- It reads requests and writes responses with `Surl.HttpMessage`'s request reader and response
  writer (ADR-0070), so a proxy request is judged by exactly the HTTP/1.x rules the HTTP server uses.
- It is not `Surl.HttpServing` (ADR-0084): a proxy relays bytes to an origin instead of answering a
  request with an `HttpAnswer`, and needs neither `Surl.Content` nor the upgrade takeover.

### 2. Reaching the origin without a socket: `IOriginConnector`

`IDataConnectionOpener.ConnectActiveAsync` does not fit: it takes an `IPEndPoint`, resolves no name,
and refuses every address but the control connection's peer, which is FTP's rule, not a proxy's.
A new contract in `Surl.Protocol.Abstractions`, shared with the SOCKS server (BL-557's family):

```csharp
public interface IOriginConnector
{
    // host: a DNS name, an IPv4 literal or an IPv6 literal without brackets.
    ValueTask<IConnection> ConnectAsync(
        string host, int port, TimeSpan timeout, CancellationToken cancellationToken);
}
// throws OriginConnectionException with an OriginConnectionFailure:
//   HostNotFound, NotAllowed, Refused, Unreachable, TimedOut
```

- `Surl.Networking` implements it (BL-551): it resolves the name with `System.Net.Dns`, **keeps only
  the addresses the allowed-origin rule admits**, and connects to them in order until one accepts.
  Resolving before checking is what stops a name that resolves to a forbidden address (DNS
  rebinding) from getting past the rule. An in-memory implementation in `Surl.Protocol.Abstractions`
  serves the proxy's unit tests.
- **The allowed-origin rule, secure by default:** only loopback (`127.0.0.0/8`, `::1`, an
  IPv4-mapped loopback compared as IPv4) unless `--proxy-allow-origin` widens it (decision 7). The
  rule is a value the composition root builds from the command line and hands to the
  `Surl.Networking` implementation; the proxy itself never decides an address.
- **The proxy's own endpoint is never an origin:** a target naming the proxy's own local address and
  port is refused as `NotAllowed`, so a request cannot loop through the proxy.
- The timeout is `ExchangeLimits.HeadTimeout`, as FTP's active data connection already uses
  (`FtpDataConnections`): an origin that does not accept within it is `TimedOut`.

How each failure is answered (RFC 9110 sections 15.5.4, 15.6.3 and 15.6.5), with the HTTP server's
fixed-text body style (ADR-0006 section 3) and one log line naming the failure, never the bytes:

| `OriginConnectionFailure` | Status | Log |
| --- | --- | --- |
| `NotAllowed` | `403 Forbidden` | `Origin <host>:<port> is not an allowed origin; answered 403.` |
| `HostNotFound` | `502 Bad Gateway` | `Origin <host> did not resolve; answered 502.` |
| `Refused`, `Unreachable` | `502 Bad Gateway` | `Origin <host>:<port> could not be reached (<failure>); answered 502.` |
| `TimedOut` | `504 Gateway Timeout` | `Origin <host>:<port> did not accept in time; answered 504.` |

Curl reads each as row 11 (forwarded: status and body, exit 0) or rows 4 and 5 (tunnel: exit 7,
`CONNECT tunnel failed, response <status>`).

### 3. Forwarding absolute-form requests

- **The target** is RFC 9112 section 3.2.2's absolute-form (row 1). The proxy connects to its
  authority (port 80 when absent) and sends the request in origin-form (section 3.2.1): path and
  query unchanged, `Host` as the request carried it. An origin-form request to the proxy (no scheme)
  is not a proxy request: `400 Bad Request`.
- **Schemes:** `http` is forwarded. `ftp` (row 12) is answered `501 Not Implemented` until BL-594
  hand-writes the FTP client half inside this library; that is sequencing, not a refusal of the
  feature. `https` and every other scheme are answered `501`: curl always tunnels `https` with
  `CONNECT` (row 13 and the manpage's `--proxytunnel`), so no curl run sends one.
- **Headers removed** before forwarding, in both directions (RFC 9110 section 7.6.1): `Connection`
  and every field it names, `Proxy-Connection` (curl sends it on every proxy request, rows 1 to 13),
  `Keep-Alive`, `TE`, `Trailer`, `Transfer-Encoding` (the body is re-framed, below), `Upgrade` unless
  the request is an upgrade, and the consumed `Proxy-Authorization`; from the origin's response,
  `Proxy-Authenticate` and `Proxy-Authentication-Info` too.
- **Headers added:** `Via: 1.1 surl` to the forwarded request and to the response curl receives, as
  RFC 9110 section 7.6.3 requires of a proxy; the pseudonym `surl` follows ADR-0006 section 3 (no
  version). `Max-Forwards` on `OPTIONS` and `TRACE` is honoured (section 7.6.2): `0` is answered by
  the proxy (`200` with `Allow` for `OPTIONS`, the request echoed for `TRACE`), anything else is
  decremented.
- **Bodies:** a `Content-Length` body is streamed through; a chunked body is de-chunked and sent on
  chunked. `Expect: 100-continue` is forwarded and the origin's `100` relayed. A response body is
  relayed with the origin's framing; a close-delimited one is relayed and curl's connection then
  closed, since its end is the only framing it has.
- **Upgrade:** a request with `Upgrade` is forwarded with it; a `101` makes the connection a byte
  relay as decision 4's tunnel.
- **Persistent connections on both sides:** curl's connection stays open per RFC 9112 section 9.3
  (curl asks with `Proxy-Connection: Keep-Alive`). The origin connection is kept while the next
  request names the same `host:port`, and replaced when it names another; closing curl's connection
  closes the origin's. An origin that closes between requests is reconnected once for an idempotent
  method, otherwise answered `502`.
- **A bad origin:** a response head the proxy cannot parse, or over `--max-request-head`, is answered
  `502`; one that does not arrive within `--head-timeout` is answered `504`.

### 4. `CONNECT`

- **Authority-form** only (RFC 9112 section 3.2.3; rows 2 to 7): `host:port`, an IPv6 literal in
  brackets. A missing or zero port, or any other form, is `400 Bad Request`.
- **Which ports:** every port from 1 to 65535 on an allowed origin is tunnelled. RFC 9110
  section 9.3.6 asks a proxy to restrict ports so it is not an open relay; Surl restricts the origin
  instead (decision 2), loopback by default, which bounds the same abuse while letting upstream's
  cases tunnel to whatever port their servers took. A target the rule refuses, or the proxy's own
  endpoint, is `403 Forbidden`; curl exits 7 with `CONNECT tunnel failed, response 403` (row 4).
- **The reply** once the origin accepted is exactly `HTTP/1.1 200 Connection established\r\n\r\n`:
  no `Content-Length` or `Transfer-Encoding`, which RFC 9110 section 9.3.6 forbids on a `2xx` to
  `CONNECT`, and no other field. Row 2 measured curl reading exactly these bytes and going on to send
  its request inside the tunnel.
- **The tunnel** relays bytes both ways unchanged and unread: TLS inside it (`https://` through
  `-p`, row 6's URL) is passed through, never terminated. When one side shuts its sending half, the
  proxy shuts the other side's; when both have, or either resets, both are closed. Bytes moving
  either way keep the exchange from idling out.
- A refused `CONNECT` (`407`, `403`, `502`, `504`) keeps curl's connection open when the reply allows
  it, because curl retries the authenticated `CONNECT` on the same connection (row 7).

### 5. `Proxy-Authorization`

- **Methods:** Basic (RFC 7617), Digest (RFC 7616), NTLM (MS-NLMP) and Negotiate (RFC 4559), checked
  by the same `Surl.Authentication` checks the HTTP server uses, with `407 Proxy Authentication
  Required`, `Proxy-Authenticate` and `Proxy-Authorization` in place of `401`, `WWW-Authenticate` and
  `Authorization` (RFC 9110 sections 11.7.1, 11.7.2 and 15.5.8). The challenges, their order
  (`negotiate`, `ntlm`, `digest`, `basic`), realm `surl` and the default set (Digest and Basic; NTLM
  and Negotiate only when named) are ADR-0032 section 3's and section 4's, so the proxy needs no new
  policy. Bearer and Signature Version 4 are not offered: curl has no proxy option for either.
- **Accounts on the command line:** proxy-specific names, mirroring curl's `-U`/`--proxy-user`:
  `-U`/`--proxy-user <user:password>`, `--proxy-user-file <file>` (ADR-0032 section 2's format) and
  `--proxy-auth <methods>` (ADR-0032 section 3's words). They are separate from `-u`, `--user-file`
  and `--auth`, because a proxy's accounts and the origin's are different people's in every curl run
  that uses both (`-u` and `-U` together).
- **When it is asked:** with no proxy account, the proxy takes no login and relies on the origin rule
  (decision 2). With one, every request and every `CONNECT` without valid credentials gets the `407`;
  `--allow-anonymous` waives it as ADR-0032 section 4 does for HTTP. Basic over the plain `http://`
  proxy needs `--allow-plaintext-auth`, as it does on the HTTP server; over the HTTPS proxy the TLS
  covers it.
- **Measured quirks the checks honour:** curl sends Basic unasked with `-U` (row 8); `--proxy-anyauth`
  first sends nothing and answers the `407` on the same connection (rows 7 and 9); its Digest `uri` is
  the target's path and query (`/a`), not the absolute-form (row 9), so the Digest check accepts the
  path of the request target as well as the target itself, and BL-553 measures `CONNECT`'s `uri`
  before pinning it; NTLM and Negotiate are bound to curl's connection (row 10 starts NTLM unasked),
  so their state lives with curl's connection and survives a change of origin.

### 6. The HTTPS proxy

- An `https://` proxy listen URL is the same server behind TLS: `Surl.Networking`'s server TLS
  handshake (`ServerTlsHandshake`) runs before the first request, exactly as for an `https` listen
  URL, and the proxy then reads row 13's plain bytes from the TLS stream.
- **The certificate** is ADR-0010's: `--cert` and `--key`, or `--self-signed`, the options every TLS
  listener shares. Curl trusts it with `--proxy-cacert` or skips the check with `--proxy-insecure`
  (row 13 measured `--proxy-insecure` with the Schannel reference build, exit 0).
- **ALPN:** `http/1.1` only for now. `--proxy-http2` makes curl offer `h2` to the proxy; a proxy that
  picks `http/1.1` is a proxy curl accepts. BL-595 serves HTTP/2 to the proxy (RFC 9113 section 8.5's
  `CONNECT`) once `Surl.Http2` exists (BL-409), and amends this decision.
- **TLS inside a tunnel** is passed through unchanged (decision 4): an `https://` origin through the
  HTTPS proxy is TLS inside TLS, the outer layer the proxy's and the inner one the origin's.

### 7. The command line, and the limits

- **The listen URL:** `-x`/`--proxy <url>`, curl's own names, repeatable, each naming one proxy
  listener: `surl --proxy http://127.0.0.1:3128` for the HTTP proxy, `surl --proxy
  https://127.0.0.1:3129 --self-signed` for the HTTPS proxy. A bare `http://` argument stays the HTTP
  server (ADR-0007), which is why the proxy needs an option. Default ports are curl's: 1080 for an
  `http://` proxy and 443 for an `https://` one (curl's manpage, `-x`). Any other scheme given to
  `--proxy` is `UnsupportedProtocol` (1), as an unknown listen URL scheme is (ADR-0005); `socks*`
  schemes join it in BL-557's ADR.
- **`--proxy-allow-origin <address[/prefix]>`**, repeatable, adds addresses to decision 2's rule;
  `--proxy-allow-origin '*'` admits every origin and is a loosening option that warns on every start
  (ADR-0032 section 5).
- Every ADR-0006 limit applies to the proxy: `--max-connections` and `--max-connections-per-address`
  count curl's connections (an origin connection belongs to the exchange that opened it, as FTP's data
  connection does); `--idle-timeout` and `--max-time` cover forwarding and tunnels alike;
  `--head-timeout` bounds curl's request head (`408`), the origin's connect and the origin's response
  head (`504`); `--max-request-head` bounds curl's request head (`431`, ADR-0006 section 5) and the
  origin's response head (`502`); `--max-filesize` bounds a forwarded request body (`413`). A tunnel's
  bytes are not a body and are bounded only by the time limits.
- BL-555 composes the server in `Surl.Console`, adds the options to `Surl.Cli` with their `--aihelp`
  facts, a `proxy` help category and topic with its prose and example (root `CLAUDE.md`, `--aihelp`).

### 8. How the upstream cases' server half plays `http-proxy` and `https-proxy`

Upstream's test HTTP server (`sws`) is itself the proxy in these cases: curl is pointed at it with
`-x`, it answers a forwarded request with the case's `<data>` without connecting anywhere, and it
answers `CONNECT` with the case's `<connect>` part and then serves the tunnelled request from `<data>`
(FILEFORMAT.md at the tag; ADR-0080's "What a `<reply>` is"). The `https-proxy` cases put upstream's
TLS front in front of it. Surl plays the same roles:

- `Surl.Protocol.HttpProxy` gains ADR-0080 decision 2's optional seam,
  `HttpProxyUpstreamTestCaseReplies`, which `Surl.Console` never composes. With it, a **forwarded
  request** is answered by the proxy itself from the case's `<data>` part (and `<dataNUM>` chosen by
  the same rules as the HTTP server half), and no origin is connected; the request bytes are recorded
  for `<protocol>`.
- A **`CONNECT`** is answered from the case's `<connect>` part (`<connectNUM>` by the same rules,
  which is how a case's `407` then `200` is played), and when that part is a `2xx` the tunnel goes to
  the origin through an in-memory `IOriginConnector` whose far end `Surl.Conformance.UnitTests` serves
  with BL-391's HTTP server half and the same case. The proxy library therefore never references the
  HTTP server, and the conformance project composes the two, as it composes every server half.
- **`https-proxy`** cases add decision 6's TLS with a certificate generated from the case's `.prm`
  (ADR-0080 decision 2), as the HTTPS server half does.
- BL-556 does this and records the 29 cases in the pass rate (BL-393); 715 and 2055 stay
  `server-not-built` until the SOCKS5 server exists.

## Consequences

- curl's proxy options have a server to talk to, loopback-only and login-free by default, widened and
  locked down only by named options.
- `Surl.Protocol.Abstractions` gains `IOriginConnector`, which the SOCKS server reuses, so BL-551 runs
  apart from every protocol task, as a contract change should.
- BL-552's `touches` already names the new projects; BL-555's names `Surl.Cli` and `Surl.Console`; no
  task's `touches` changes because of this ADR.
- Two pieces are sequenced, not dropped: `ftp://` forwarding (BL-594) and HTTP/2 to the HTTPS proxy
  (BL-595).

## Alternatives considered

- **Reusing `IDataConnectionOpener`.** Rejected: it resolves no name and carries FTP's
  same-peer rule; widening it would make FTP's data connections able to reach other hosts.
- **Putting the proxy in `Surl.Protocol.Http`.** Rejected: the HTTP server answers from a content
  store and the proxy from an origin; one class library holding both would put the origin connector
  in reach of every HTTP request, and the conformance seams would tangle.
- **An open proxy by default.** Rejected: a proxy that reaches any address on a public listener is an
  open relay. Loopback covers every curl test and every local use; anything wider is named.
- **Restricting `CONNECT` to port 443.** Rejected: upstream's cases tunnel to arbitrary ports, and the
  origin rule bounds abuse already.
- **Sharing `-u`, `--user-file` and `--auth` with the HTTP server.** Rejected: one surl serving both
  an origin and a proxy (as curl tests with `-u` and `-U` together) needs two sets of accounts.

## Amendment 1 - what BL-552 settled inside decisions 3 and 4 (2026-11-05)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-552, which
built decisions 1, 3 and 4. Each refines a decision without changing what curl sees; BL-552
re-measured rows 1 and 2 and the `403` and `502` answers with `Record-CurlExchange.ps1` against the
Windows reference build (`Surl.Protocol.HttpProxy.UnitTests/Fixtures`).

- **`Host` is generated from the target's authority**, replacing the received one in place (or
  leading the fields when there was none), instead of being sent "as the request carried it":
  RFC 9112 section 3.2.2 says a proxy MUST. curl always sends the authority as `Host` (rows 1 and
  12), so the bytes the origin receives from curl are unchanged. A request with more than one
  `Host` is `400` (RFC 9112 section 3.2).
- **`Proxy-Authorization` is always removed**, consumed or not: credentials for the proxy are never
  the origin's. **Trailer fields** of a chunked body are read and dropped in both directions, as
  `Trailer` is removed. **`Max-Forwards`** is decremented only on `OPTIONS` and `TRACE`, the methods
  RFC 9110 section 7.6.2 defines it for; on others it passes unchanged. The local `OPTIONS` answer's
  `Allow` is `CONNECT, DELETE, GET, HEAD, OPTIONS, PATCH, POST, PUT, TRACE`; the local `TRACE`
  answer echoes the request head as `message/http`.
- **Answers the proxy makes itself** carry `Date`, `Server: surl`,
  `Content-Type: text/plain; charset=us-ascii` and the body `<code> <reason>` and a line feed.
  curl's connection stays open after an unreachable-origin answer, a `501` for another scheme and a
  local `OPTIONS` or `TRACE` answer, unless the request was HTTP/1.0, said `close` in `Connection`
  or `Proxy-Connection`, or carried a body; a malformed head, target or body framing (`400`, `431`,
  `408`, `505`, `501` for a `Transfer-Encoding` other than `chunked` alone, `413`) closes it.
- **The origin's response** is relayed as `HTTP/1.1` with the origin's status and reason. A body
  with disagreeing or malformed `Content-Length` fields is `502`. A `Transfer-Encoding` whose last
  coding is not `chunked` is relayed close-delimited. A response body that ends early or is
  malformed aborts curl's connection, since its head has already gone. An origin response with
  `Connection: close`, or from HTTP/1.0, drops the origin connection after it.
- **The reconnect** happens when a kept origin connection fails on the write or closes before a byte
  of its response, for `GET`, `HEAD`, `OPTIONS`, `TRACE`, `PUT` and `DELETE` without a body.
- **`Expect: 100-continue`**: the proxy waits for the origin's first response head before sending
  the body; a `100` is relayed and the body follows, a final answer is relayed and curl's
  connection closes after it, its body unsent.
- **`Schemes`** is `http` and `https`: the proxy is chosen by `--proxy`, not by scheme (decision 7),
  so BL-555 composes it beside the HTTP server rather than through the scheme table.

## Amendment 2 - what BL-554 settled inside decision 6 (2026-11-05)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-554, which
built decision 6. Measured with `Record-CurlExchange.ps1 -Tls -TlsAlpn` against the Windows
reference build (`0E773709…8778`) and, for `--proxy-http2`, the pinned static 8.21.0 build
(`589C8E4D…2648`, the Windows build with HTTP/2; ADR-0081):

| curl arguments (after `-sS`) | Recorder offered | Agreed | Exit |
| --- | --- | --- | --- |
| `-x https://127.0.0.1:<port> --proxy-insecure http://origin.test/a?b` (reference build) | `h2,http/1.1` | `http/1.1` | 0 |
| the same (static build) | `h2,http/1.1` | `http/1.1` | 0 |
| `--proxy-http2 -x https://… --proxy-insecure http://origin.test/a` (static build) | `h2,http/1.1` | `h2` | 16 (the recorder spoke no HTTP/2) |
| `--proxy-http2 -x https://… --proxy-insecure http://origin.test/a?b` (static build) | `http/1.1` | `http/1.1` | 0, row 13's bytes |

- **A proxy listen URL is marked, and its ALPN is `http/1.1` alone.** Curl offers `http/1.1` to an
  HTTPS proxy, and `h2` first with `--proxy-http2`; a listener offering an `https` origin's
  `h2, http/1.1` (ADR-0081 decision 2) would agree `h2` with the latter, which the proxy does not
  serve until BL-595. So `ListenUrl` gains `IsProxy` (an init-only property, `false` unless set:
  every existing listen URL keeps its behaviour), set by whoever builds a listen URL from
  `--proxy` (BL-555), and `Surl.Networking`'s `TlsApplicationProtocols.ForListener` offers
  exactly `http/1.1` for an `https` proxy listen URL. `ServerTlsHandshake.ForListener` uses it.
  The proxy, not the scheme, decides, because the scheme is `https` for both (decision 7).
- **The handshake is the engine's**, as for any `https` listen URL (ADR-0010 section 2): it runs
  before `ServeAsync` within the head timeout and is noted `TLS handshake completed: …`; a failed
  one is noted `TLS handshake failed: <message>`, nothing is written and the proxy never sees the
  connection.
- **No ALPN in common** (a client offering only `h2`) fails the handshake on Windows as on Linux:
  Schannel answered BL-554's fast test with "No common application protocol exists between the
  client and the server", where ADR-0010 section 6's table expected Windows to complete without
  ALPN. No pinned curl build reaches it (curl always offers `http/1.1`). macOS is not pinned.
- **What the proxy itself logs.** On a connection secured before its first request the proxy
  notes once, before reading: `HTTPS proxy: curl's connection to the proxy is TLS (<protocol>,
  ALPN <id or none>); requests are read inside it.` A plain connection gets no such note. A
  session that agreed any ALPN but `http/1.1` (none is served too, as HTTP/1.1) is noted
  `HTTPS proxy: the TLS handshake agreed ALPN <id>, which the proxy does not serve; the
  connection closes.` and closed without a byte written or an origin connected; it is reachable
  only by a listener composed without `IsProxy`.
- **Forwarding and `CONNECT` are unchanged** inside the TLS: the bytes curl sends inside it are
  row 13's, byte for byte row 1's, and a tunnel's bytes, a nested TLS record stream included, are
  relayed unread (decision 4).

## Amendment 3 - what BL-553 settled inside decision 5 (2026-11-05)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-553.

Measured with `Record-CurlExchange.ps1` against the Windows reference build (SHA-256
`0E773709…8778`), the recorder playing the proxy and answering with the bytes Surl's proxy now
writes (`Protocols/Http/Surl.Protocol.HttpProxy.UnitTests/Fixtures`, `proxy-*`):

| Case | curl arguments (after `-sS`) | Exit | What curl sent |
| --- | --- | --- | --- |
| No credentials, forwarded | `-x … http://origin.test/a` | 0, curl printed the `407`'s body | the `GET` without credentials |
| No credentials, `CONNECT` | `-p -x … http://origin.test:8080/a` | 7, `CONNECT tunnel failed, response 407` | the `CONNECT` without credentials |
| Basic, `CONNECT` | `-p -U u:p -x …` | 0 | `Proxy-Authorization: Basic dTpw` on the `CONNECT` unasked; none inside the tunnel |
| Digest, forwarded | `--proxy-anyauth -U u:p -x … http://origin.test/a?b` | 0 | after the `407`, on the same connection, Digest with `uri="/a?b"` |
| Digest, `CONNECT` | `-p --proxy-digest -U u:p -x … http://origin.test:8080/a` | 0 | after the `407`, on the same connection, Digest with **`uri="origin.test:8080"`: the authority, the `CONNECT`'s own target** |
| NTLM, forwarded and `CONNECT` | `--proxy-ntlm -U u:p` (with and without `-p`) | 0 | the NEGOTIATE_MESSAGE unasked; after a `407` carrying a CHALLENGE_MESSAGE, the AUTHENTICATE_MESSAGE on the same connection: NTLM's three legs whole |

What this settles:

- **No reference to `Surl.Authentication`.** The proxy takes an `IAuthenticationPolicy` - the one
  built from the proxy's own accounts (`-U`, `--proxy-user-file`, `--proxy-auth`), composed by
  BL-555 - exactly as the HTTP server does, so it needs no new reference and the isolation test no
  new row. `HttpProxyProtocolServer(originConnector)` passes `AnonymousAuthenticationPolicy`: the
  "no proxy account, no login" case of decision 5, which BL-555 composes when no proxy account is
  configured. `--allow-anonymous` and `--allow-plaintext-auth` stay the policy's, unchanged.
- **One session per curl connection,** started before the first request with the connection's
  `TlsSession`, so NTLM's and Negotiate's legs live with curl's
  connection and survive a change of origin.
- **What the session is given.** The request's fields with every `Authorization` removed (it is
  the origin's login and is forwarded untouched) and every `Proxy-Authorization` renamed
  `Authorization`; as the target, a `CONNECT`'s authority as received and an absolute-form
  request's path and query (a target that is not absolute-form, as received). These are exactly
  the Digest `uri` values measured above, so the Digest check's exact `uri` comparison passes
  unchanged and decision 5's "path as well as the target itself" is not needed: curl never sends
  the absolute-form as `uri`.
- **The order** is: head read, the `Host` check, then the login, then the target's checks. A
  request that is not let in learns nothing about the target's form or the origin.
- **The answers.** A challenge is `407 Proxy Authentication Required` with `Date`, `Server: surl`,
  one `Proxy-Authenticate` per challenge in the policy's order, and the fixed-text body; it keeps
  curl's connection, which curl's `--proxy-anyauth`, Digest and NTLM all need, unless the request
  asked to close or announced a body the proxy does not read (`Connection: close` then). A
  refusal (`Forbidden`) is `403` and closes, as on the HTTP server. A checked login's note is
  logged; the credentials never are.
- **Upstream cases.** Of decision 5's 24 `http-proxy` cases, 80, 275 and 1849 send
  `Proxy-Authorization` (Basic, in their `<proxy>` or `<protocol>` sections); the rest send none.
