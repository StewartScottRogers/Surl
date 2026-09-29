# ADR-0006 — Hardening for internet-facing use

- **Status:** Accepted
- **Date:** 2026-09-28
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-28

## Context

Stewart answered the product overview's open question 3 on 2026-09-28: Surl is built to
be exposed to the internet, not only to answer on loopback in a test. Until then nothing
bounded what a peer may make `surl` hold: connections, time, memory, disk, or what a
reply reveals. This ADR fixes the security posture as numbers and rules a test can pin,
so the command-line ADR (BL-003) carries each configurable limit as an option and the
serving tasks build it in.

Two rules bound every choice:

- **Fidelity to upstream curl stays the target** (product overview, "Non-goals"). A
  default limit must never make an exchange that pinned upstream curl 8.21.0 makes with
  its own defaults fail. Where an unusual but legitimate curl invocation could exceed a
  default, the limit is configurable, and 0 turns it off.
- **Base class library only.** Every limit is a counter, a size check or a timer on the
  exchange context's `TimeProvider` (ADR-0004, section 5); no package.

Inputs: the listener seam and exchange context (ADR-0004), the exit-code table
(ADR-0005), and `Surl.Content.UnitLibrary/CLAUDE.md` (a request path never escapes the
content store's root).

### What upstream curl 8.21.0 does by default

From the pinned build's own manual (`curl --manual`, recorded with
`Record-CurlExchange.ps1 -NoServer`, 2026-09-28; build
`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`):

| curl default | Value | What it bounds on Surl's side |
| --- | --- | --- |
| `--parallel-max` | 50 transfers at once with `-Z`, up to 65535 | concurrent connections from one address |
| `--parallel-max-host` | 0 (unlimited) | concurrent connections from one address |
| `--max-time` | none: a transfer may take as long as it takes | maximum exchange duration |
| `--keepalive-time` | 60 seconds of idle before TCP keepalive probes | nothing: TCP probes carry no data and do not reset Surl's idle timer |
| `--expect100-timeout` | 1 second waiting for `100 Continue` | nothing: curl sends the body anyway |

libcurl keeps an idle connection in its cache for reuse for up to 118 seconds
(`CURLOPT_MAXAGE_CONN`, https://curl.se/libcurl/c/CURLOPT_MAXAGE_CONN.html, read
2026-09-28). A connection Surl closes sooner is not a failure - curl notices the dead
connection and opens a new one - but an idle timeout above 118 seconds never makes it
retry.

### What upstream curl 8.21.0 negotiates over TLS

Measured with `Record-CurlExchange.ps1 -Tls -TlsProtocol …` (the `-TlsProtocol`
parameter was added by BL-024 for this), same build, `https://127.0.0.1:18443/`,
2026-09-28:

| Server accepts | curl arguments | Exit code | First line of stderr |
| --- | --- | --- | --- |
| TLS 1.2 only | `-sS -k` | 0 | (none) |
| TLS 1.3 only | `-sS -k` | 0 | (none) |
| TLS 1.2 and 1.3 | `-sS -k -v` | 0 | (verbose; `ALPN: curl offers http/1.1`) |
| TLS 1.2 and 1.3 | `-sS -k --tls-max 1.1` | 35 | `curl: (35) schannel: failed to receive handshake, SSL/TLS connection failed` |
| TLS 1.2 only | `-sS -k --tlsv1.3` | 35 | `curl: (35) schannel: failed to receive handshake, SSL/TLS connection failed` |

So the pinned build negotiates TLS 1.2 and TLS 1.3 with its defaults, and needs an
explicit `--tls-max 1.1` or lower to ask for anything older.

### What upstream curl 8.21.0 reports for a refusal

Same build and tool, plain HTTP, 2026-09-28:

| Server sends | curl arguments | Exit code | stderr |
| --- | --- | --- | --- |
| `HTTP/1.1 503 Service Unavailable`, `Content-Length: 0`, `Connection: close` | `-sS -f` | 22 | `curl: (22) The requested URL returned error: 503` |
| nothing, then closes | `-sS` | 52 | `curl: (52) Empty reply from server` |
| `HTTP/1.1 431 Request Header Fields Too Large`, `Content-Length: 0`, `Connection: close` | `-sS -o NUL -w %{http_code}` | 0 | (none); stdout `431` |

A status line tells curl's user why; a bare close only says "empty reply". Surl
therefore answers a refusal in the protocol's own words wherever the protocol has them.

## Decision

Every numeric limit below takes a whole number; durations are in seconds and accept a
decimal fraction with a dot, as curl's own time options do. **0 means no limit.** A
negative, non-numeric or out-of-range value is a command-line error: `FailedInit` (2,
ADR-0005). No limit being reached ever ends the `surl` process or changes its exit code;
it ends one connection or one exchange, and the verbose log notes which limit and why.

The option names below are the ones BL-003's option table carries. Where upstream curl
has an option with the matching meaning on the client side, surl keeps curl's name
(`--max-time`, `--max-filesize`, `--tlsv1.2`, `--tls-max`); the rest are Surl's own.

### 1. Resource limits

| Limit | Default | Option | Enforced by |
| --- | --- | --- | --- |
| Concurrent connections and datagram flows, all listeners together | 1024 | `--max-connections <n>` | `Surl.Core` |
| Concurrent connections and datagram flows from one remote IP address | 100 | `--max-connections-per-address <n>` | `Surl.Core` |
| Exchange idle timeout | 120 s | `--idle-timeout <seconds>` | `Surl.Core` |
| Maximum exchange duration | 3600 s | `--max-time <seconds>` (`-m`) | `Surl.Core` |
| Head timeout (slow-sender defence) | 30 s | `--head-timeout <seconds>` | each protocol server, from `ExchangeLimits` |
| Maximum request head: HTTP/1.x and RTSP | 102400 bytes (100 KiB) | `--max-request-head <bytes>` | `Surl.Protocol.Http`, `Surl.Protocol.Rtsp` |
| Maximum command line: FTP, SMTP, IMAP, POP3, DICT, Gopher | 8192 bytes, line ending included | `--max-line <bytes>` | each line-oriented protocol server |
| Maximum framed message: MQTT packet, LDAP message, SMB message, SSH packet, WebSocket frame, HTTP/2 and HTTP/3 header block | 1048576 bytes (1 MiB) | `--max-message <bytes>` | each binary-framed protocol server |
| Maximum upload: a request body, an uploaded file, an SMTP message, an MQTT `PUBLISH` payload | 104857600 bytes (100 MiB) | `--max-filesize <bytes>` | each protocol server that receives one, checked again by `Surl.Content` as it writes |

What each limit means:

- **Connections.** An IP address is compared after mapping an IPv4-mapped IPv6 address
  to IPv4. A datagram flow (TFTP, ADR-0004 section 3) counts as one connection. The
  defaults leave room for two default `curl -Z` runs from one address at once (50 each)
  and for about ten such peers in total; a test harness that needs more raises them.
- **Idle timeout.** An exchange is idle while no byte moves in either direction on any
  of its transports. Bytes on an FTP data connection belong to the exchange that opened
  it, so a long FTP download never idles its control connection out. A peer that stops
  reading is idle too: a write that makes no progress for the idle timeout ends the
  exchange. 120 s is above libcurl's 118-second connection-cache age, so a connection
  curl may still reuse is never closed under it. An interactive TELNET session idles
  out after 120 s unless the option raises it.
- **Maximum exchange duration.** Measured from accept (or a flow's first datagram) to
  the end of `ServeAsync`. Upstream curl sets no transfer time limit, so an hour is a
  guard against a peer that trickles one byte before every idle timeout, not a
  transfer budget; a longer transfer raises it or passes 0.
- **Head timeout.** The time a peer has to deliver a complete request head (HTTP, RTSP)
  or command line (the line-oriented protocols), or first packet (the binary-framed
  ones). On a new connection the clock starts at accept and includes the TLS handshake,
  so a peer that never finishes a handshake is dropped after it too. On a kept-alive
  connection, and between commands, it starts at the first byte of the next head or
  line; until then the idle timeout governs. It does not apply to request bodies or
  uploads, and not to TELNET, whose lines a person types.
- **Sizes.** A head or line is counted from its first byte to its terminating
  `CRLF CRLF` or `CRLF` inclusive. TFTP needs no size limit: its request is one datagram
  and a datagram is bounded by the transport. `--max-filesize` keeps curl's name and
  turns its meaning around: curl refuses to download more than it, surl refuses to
  receive more than it. A server that can say the limit before the body arrives does so
  (SMTP `SIZE` in its `EHLO` reply, HTTP answering `Expect: 100-continue` with the
  refusal instead of `100 Continue`).

### 2. What a server exposes by default

| Exposure | Default | Option that changes it |
| --- | --- | --- |
| Uploads (HTTP `PUT` and `POST` bodies written to the store, FTP `STOR`/`APPE`, TFTP `WRQ`, SFTP and SCP writes, SMB writes) | **off**: refused | `--allow-uploads` |
| Directory listings (an HTTP directory index, FTP `LIST`/`NLST`/`MLSD`, a Gopher menu of a directory, SFTP `READDIR`, SMB directory queries) | **off**: refused | `--list-directories` |
| Symbolic links, junctions and other reparse points inside the content store's root | **not followed**: answered as absent | `--follow-symlinks` follows one only when its final target resolves inside the root |
| Dot-files: any path with a segment that starts with `.` | **hidden**: answered as absent, and left out of every listing | `--serve-dot-files` |

- A link whose target resolves outside the root is refused whatever the options
  (`Surl.Content.UnitLibrary/CLAUDE.md`).
- "Answered as absent" means the protocol's not-found answer, identical to the answer for
  a path that does not exist, so a peer cannot tell a hidden entry from a missing one.
  A refused listing is answered as absent too. A refused upload gets the protocol's
  "not permitted" answer (HTTP 405 with an `Allow` header, FTP 550, TFTP error 2).
- Each listen URL names the address it binds; surl binds every interface only when the
  URL says `0.0.0.0` or `[::]`, never by default. BL-003 decides the listen-URL grammar
  and must not introduce a host-less URL that binds every interface.

### 3. What a peer may learn

- **Error text sent to a peer is fixed text chosen by the protocol server**: a status
  line and reason phrase, a reply code and a short sentence from the server's own table.
  It never contains a local file-system path, the content store's root, an exception
  message or type, a stack trace, an operating-system error text or number, or a user
  name of the host. A path the peer itself sent may be echoed only after rendering as in
  the next point.
- **No version number in a banner.** A server may name itself `surl` (HTTP `Server:
  surl`, an FTP `220` greeting) but never states surl's, .NET's or the operating
  system's version. `surl --version` on the command line is the place for that.
- **Untrusted bytes are escaped in the verbose log.** Every byte that came from a peer
  (in `BytesReceived`, and in any `Note` text built from a peer's bytes: a path, a
  header value, a user name) is rendered as itself only when it is printable ASCII,
  0x20 to 0x7E, except backslash. Every other byte - C0 controls including ESC, DEL,
  and every byte from 0x80 up - and backslash itself are rendered as `\xHH` with two
  upper-case hex digits; CR and LF are rendered as `\r` and `\n`, and the log may start
  a new line after `\n`. No byte a peer chose therefore reaches the operator's terminal
  as a control sequence, and the rendering is reversible. BL-003 fixes the rest of the
  log line format. Bytes Surl sends are rendered the same way, so one rule covers both
  directions.
- The verbose log is the operator's, and may name local paths and exception messages.

### 4. TLS minimums

- `Surl.Networking` accepts **TLS 1.2 and TLS 1.3** by default, the two versions pinned
  upstream curl 8.21.0 negotiates with its own defaults (measured above). SSL 2, SSL 3,
  TLS 1.0 and TLS 1.1 are refused.
- The options keep curl's names and set the same bounds from the server's side:
  `--tlsv1.0`, `--tlsv1.1`, `--tlsv1.2` (the default) and `--tlsv1.3` set the lowest
  version accepted; `--tls-max <1.0|1.1|1.2|1.3>` sets the highest. A lowest version
  above the highest is `FailedInit` (2). Lowering the minimum is the operator's choice;
  whether the operating system's TLS stack will then negotiate TLS 1.0 or 1.1 is the
  platform's (Windows 11 Schannel disables both on the server side by default), and
  surl does not work around it.
- Cipher suites are the operating system's defaults. .NET's `CipherSuitesPolicy` is
  not supported on Windows, and one rule must hold on all three platforms.
- The TLS handshake is inside the head timeout (section 1).
- **Constraints on BL-002's TLS contract:** the server-side TLS options carry the
  accepted versions as `System.Security.Authentication.SslProtocols` built from the two
  options above; the handshake takes the exchange's head-timeout cancellation; the
  server side sets `SslServerAuthenticationOptions.AllowRenegotiation` to `false`
  (client-initiated renegotiation is a known CPU-exhaustion lever); and a failed or
  timed-out handshake ends the connection with no bytes written after the TLS alert and
  a verbose-log note. BL-012 confirms with the pinned build that curl's defaults still
  complete with renegotiation off; if they do not, a new ADR supersedes that point.

### 5. When a limit is hit

| Limit | HTTP, RTSP | FTP, SMTP | IMAP | POP3 | DICT | Gopher, TELNET, TFTP, MQTT and other framed protocols |
| --- | --- | --- | --- | --- | --- | --- |
| Too many connections (total or per address) | `503 Service Unavailable`, `Connection: close`, then close | `421` reply, then close | untagged `* BYE`, then close | `-ERR`, then close | `420` reply, then close | close with no bytes (TFTP: error packet 0 through `IDatagramRefusalWriter`, then the flow ends) |
| Head timeout | `408 Request Timeout`, `Connection: close`, then close, if any byte of the head arrived; otherwise close with no bytes | `421`, then close | `* BYE`, then close | `-ERR`, then close | `420`, then close | close with no bytes |
| Head or line too long | `431 Request Header Fields Too Large`, `Connection: close`, then close | `500`, then close | tagged `BAD` when the tag was read, else `* BYE`; then close | `-ERR`, then close | `500`, then close | close with no bytes |
| Framed message too long | (HTTP/2, HTTP/3: the protocol's own error, then close) | n/a | n/a | n/a | n/a | the protocol's own error where it has one (SSH `DISCONNECT`), then close; MQTT: close with no bytes |
| Upload too large | `413 Content Too Large`, `Connection: close`, then close | FTP `552`, SMTP `552`; the partial upload is deleted | tagged `NO` | n/a | n/a | TFTP error 3; MQTT: close with no bytes; the partial upload is deleted |
| Idle timeout, maximum duration | close with no bytes | `421`, then close | `* BYE`, then close | close with no bytes | close with no bytes | close with no bytes |

- **MQTT closes without a reply.** Upstream curl speaks MQTT 3.1.1, in which a server
  sends no `DISCONNECT` and has no reason codes (those are MQTT 5.0); 3.1.1 says a
  server that cannot go on closes the network connection, so Surl does.
- **Every hit is a graceful close**: the refusal is written with a write deadline of one
  second, then the connection completes its writes and is disposed (ADR-0004 section 2).
  Surl never reads more than the limit to be polite, and never resets a connection for
  a limit; `Abort` stays for a peer that broke the protocol.
- **A connection past a connection limit** is accepted, answered as above and closed at
  once, without a TLS handshake on a secure listener (a secure refusal is a bare close)
  and without being counted against the limits or given an `ExchangeId`. Exchanges
  already running are unaffected.
- **Exit codes.** None: hitting a limit never ends the process (see the head of this
  section). An invalid limit value on the command line is `FailedInit` (2).
- The exact reply texts are pinned by each protocol server's task, measured against
  pinned upstream curl as ADR-0003 requires; the status and reply codes above are what
  they measure.

### 6. How the limits reach the code

- **`ExchangeLimits`** (new, `Surl.Protocol.Abstractions`): a sealed record carrying
  `HeadTimeout` (`TimeSpan`, `Timeout.InfiniteTimeSpan` when 0), `MaxRequestHeadBytes`,
  `MaxLineBytes`, `MaxMessageBytes` and `MaxUploadBytes` (`long`; 0 is no limit), with a
  static `Default` holding this ADR's defaults. `ExchangeContext` gains a `Limits`
  member, the "later member" ADR-0004 section 5 leaves room for. Idle timeout and
  maximum duration are not on it: `Surl.Core` enforces them by cancelling
  `ExchangeContext.CancellationToken`, which every server already honours.
- **`IConnectionRefusalWriter`** (new, `Surl.Protocol.Abstractions`): an optional
  interface a connection protocol server implements beside `IConnectionProtocolServer`,
  `ValueTask WriteRefusalAsync(IConnection connection, ConnectionRefusal refusal,
  CancellationToken cancellationToken)`, with `ConnectionRefusal` an enum
  (`TooManyConnections`, `TooManyConnectionsFromAddress`). `Surl.Core` calls it for a
  connection past a limit on a plaintext listener; a server that does not implement it
  gets a bare close.
- **`IDatagramRefusalWriter`** (new, `Surl.Protocol.Abstractions`): the same for a
  datagram protocol server, `ValueTask WriteRefusalAsync(IDatagramFlow flow,
  ConnectionRefusal refusal, CancellationToken cancellationToken)`. `Surl.Core` calls it
  for a flow past a limit, then disposes the flow; the TFTP server answers with error
  packet 0. A server that does not implement it has the flow disposed with no reply.
- **`Surl.Content`** takes the exposure settings of section 2 and `MaxUploadBytes` as
  constructor options, so every protocol server gets the same answers.
- **`Surl.Networking`** takes the accepted TLS versions (section 4).
- **`Surl.Cli`** parses every option in sections 1, 2 and 4 (BL-003's table, BL-014's
  parser); **`Surl.Console`** hands the values to each library.

## Alternatives considered

- **Keep everything unlimited, and tell operators to put Surl behind a reverse proxy.**
  Rejected: Stewart asked for Surl itself to be hardened, and many of Surl's protocols
  (FTP, SMTP, TFTP, MQTT) have no common reverse proxy.
- **Stop accepting when a connection limit is reached, leaving new connections in the
  kernel backlog.** Rejected: curl then waits up to its 300-second connect timeout and
  learns nothing; an answer in the protocol's words says why at once.
- **Answer a hidden or refused path with 403.** Rejected: 403 confirms the entry exists.
- **Rate-limit new connections per address per second.** Not decided here: the
  concurrency, head-timeout and duration limits bound what one address can hold, and a
  volumetric flood is for the network in front of any server. A later ADR may add one.
- **An idle timeout of 60 s, as many web servers use.** Rejected: under libcurl's
  118-second connection-cache age it makes curl retry on a fresh connection, a
  difference in behaviour a conformance test would see.

## Consequences

- BL-003's option table carries every option in sections 1, 2 and 4 with these defaults.
- BL-002's TLS contract takes the constraints in section 4.
- BL-025 enforces the `Surl.Core` rows of section 1 and the connection refusals of
  section 5.
- Changing a default is a new ADR that supersedes the number here; adding a limit is a
  new ADR too.
- Follow-up implementation tasks, filed by BL-024 and listed in its Log, one per project:
  `Surl.Protocol.Abstractions` (`ExchangeLimits`, `ExchangeContext.Limits`,
  `IConnectionRefusalWriter`, `IDatagramRefusalWriter`), `Surl.Content` (exposure defaults and the upload limit),
  `Surl.Networking` (TLS versions and renegotiation), `Surl.Output` (escaped rendering
  of untrusted bytes), `Surl.Protocol.Http` (head timeout, head size, upload size and
  refusal replies), and the Phase 1 protocol servers `Surl.Protocol.Dict`,
  `Surl.Protocol.Gopher`, `Surl.Protocol.Mqtt` and `Surl.Protocol.Tftp`. The Phase 2 and
  later servers (FTP, SSH, SMTP, IMAP, POP3, WebSocket, LDAP, SMB, RTSP) take their rows
  of sections 1 and 5 as part of their own serving tasks.
