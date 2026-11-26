# ADR-0182 — What surl's `-v` log, trace dumps and `--log-file` write in place of a client's passwords and tokens

- **Status:** Accepted
- **Date:** 2026-11-20
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-20, in BL-1417. No
  package, no threshold change, no download.
- **Builds on:** [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) (secure by default; section 9, warnings),
  [ADR-0033](ADR-0033-console-log-levels-trace-dumps-and-the-log-file.md) section 4 (the levelled log and trace dumps),
  [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decision 1 (the event stream).

## Context

Surly cookbook bug report 2026-11-20, item 1: `surl -v --user alice:s3cret ftps://...` logs `PASS s3cret`, and `-v`
over HTTP shows `Authorization: Basic` and `Bearer` values. `RecordingConnection` hands every received chunk, raw,
to `EventPublishingExchangeLog`, which forwards it to `VerboseExchangeLog` (`-v`) or `TraceExchangeLog`
(`--trace`, `--trace-ascii`). Both sit above TLS, so FTPS and HTTPS leak as well. QUIC and DTLS plaintext reaches the
log as a hex note from `EventPublishingDecryptedBytesSink`.

Upstream curl 8.21.0's `--manual` (the pinned reference build) does not redact its own client-side log. Under
`--trace-ascii` it says: "Note that verbose output of curl activities and network traffic might contain sensitive
data, including usernames, credentials or secret data content." That is a client printing the secret its user just
typed. A server's log holds the secrets of every client that ever connected and is kept, attached to CI runs and
pasted into tickets. Secure by default (root `CLAUDE.md`, ADR-0032) therefore wins over mirroring curl: this is a
deliberate difference, not a fidelity gap, because no curl exchange depends on what surl writes to its own log.

Bytes arrive in arbitrary chunks before any server parses them, and the log factory does not know the scheme, so a
redactor must buffer to a line or frame boundary and sometimes needs what the server sent (after an SMTP `334` or an
IMAP `+`, the next client line is a secret).

## Decision

1. **What replaces a secret.** Exactly the secret's bytes are replaced by four asterisks, `****`, keeping the
   command or field name: `PASS ****`, `Authorization: Basic ****` (the scheme word stays, the credentials go),
   `Authorization: Bearer ****`. The user name stays visible (`USER alice`): it is not a secret and a log is
   useless without it. The secret's length is not shown: `****` is fixed, so a log never reveals it.
2. **What a trace dump counts.** `--trace` and `--trace-ascii` dump the redacted bytes and their `<n> bytes (0x<n>)`
   header counts the bytes dumped, as upstream's header counts the bytes it dumps. The wire counts are unchanged in
   `ExchangeEndedEvent` (`ReceivedByteCount`, `SentByteCount`), which count the real traffic.
3. **Sinks that redact.** `-v`, `--trace`, `--trace-ascii`, `--log-file` and `Surl.Api`'s `Log` writer, because all
   are one `IExchangeLog` chain built by the engine. Redaction is by default and every level (`-v` included).
4. **The event stream keeps raw bytes.** `ExchangeBytesEvent` carries the bytes exactly as they crossed the wire.
   Reasons: a test that seeds a login and asserts what the client sent (`Surl.Api` `Exchanges`) must see it, the
   test owns the secret, and an event is data a program reads, not a log a person keeps. Surl.Mcp's `get_exchanges`
   and the control endpoint's events are the same event stream consumed in another process; they are loopback-only
   and opt-in (a test or an agent asks for them), so they carry raw bytes too, and `--aihelp security` and the
   tool descriptions state it (BL-1436). A host that writes events to a file is the host's decision, not surl's log.
5. **No option turns redaction off.** There is nothing to loosen: a test that needs the secret reads the event stream.
   Adding an off switch later is a loosening that must warn (ADR-0032 section 9) and is a new ADR.
6. **The seam.** `Surl.Protocol.Abstractions` gains an optional interface `IExchangeBytesRedactingServer`, implemented
   by a protocol server that carries secrets, with `CreateRedactor(ExchangeRedactionContext)` returning an
   `IReceivedBytesRedactor` for one exchange (BL-1418). A server that does not implement it is logged byte for byte
   as before. No central switch lists the servers.
   - `IReceivedBytesRedactor.Redact(ReadOnlySpan<byte> received, ArrayBufferWriter<byte> output)` takes the bytes
     the server received, in order, and writes what may be logged; `Flush` writes any held-back remainder when the
     exchange ends. `ObserveSent(ReadOnlySpan<byte>)` shows it what the server sent, so a SASL line after `334` or
     `+` is known to be a secret. It sees both directions in one order because the engine drives it from the single
     log wrapper.
   - **Chunk boundaries.** A redactor holds back no more than one line (line protocols) or one frame (binary
     protocols), and never more than 64 KiB; past that it emits the remainder as `****` for the unfinished secret
     field and carries on. It never holds bytes it has recognised as not secret. Held bytes are written at `Flush`.
   - **Binary frames** (LDAP, MQTT, SOCKS5, AMQP, Kafka) are parsed by the redactor by their length fields and
     only the secret field is replaced; the rest, header bytes included, is logged as received. The dump's count is
     the new length (decision 2).
   - **Placement.** `ServingEngine.OpenExchange` asks the server for the redactor and wraps the level's log in a
     redacting `IExchangeLog` (BL-1419), applied below `EventPublishingExchangeLog`, so the event stream is fed raw
     bytes and the level's log redacted ones. `RecordingExchangeLog` and the upstream-test-case server halves in
     `Surl.Conformance` keep raw bytes: tests compare against the wire.
   - QUIC and DTLS plaintext notes (`EventPublishingDecryptedBytesSink`) go through the same redactor; the HTTP/3
     redactor works on the QPACK header block (BL-1424).
7. **Which servers carry a recoverable secret** (each with the task that redacts it):

   | Server | Field | Task |
   | --- | --- | --- |
   | The contract and the line redactor | - | BL-1418, BL-1419 |
   | FTP, FTPS | `PASS`, `ACCT` | BL-1420 |
   | HTTP/1.x head redactor | `Authorization`, `Proxy-Authorization` | BL-1421 |
   | http, https | as above | BL-1422 |
   | HTTP/2 | HPACK header blocks | BL-1423 |
   | HTTP/3 | QPACK header blocks | BL-1424 |
   | HTTP proxy | `Proxy-Authorization`, `Authorization` | BL-1425 |
   | RTSP | `Authorization` | BL-1426 |
   | WebSocket | upgrade `Authorization` | BL-1427 |
   | SASL continuation lines | base64 lines after `334` / `+` | BL-1428 |
   | SMTP | `AUTH` | BL-1429 |
   | IMAP | `LOGIN`, `AUTHENTICATE` | BL-1430 |
   | POP3 | `PASS`, `AUTH` | BL-1431 |
   | LDAP | simple bind password | BL-1432 |
   | MQTT 3.1.1 | CONNECT password | BL-1433 |
   | SOCKS5 | RFC 1929 password | BL-1434 |
   | Redis (extended) | `AUTH`, `HELLO ... AUTH` | BL-1435 |
   | NATS (extended) | `CONNECT` `pass`, `auth_token` | BL-1458 |
   | STOMP (extended) | `passcode` | BL-1459 |
   | AMQP 0-9-1 (extended) | PLAIN, AMQPLAIN | BL-1460 |
   | AMQP 1.0 (extended) | SASL PLAIN | BL-1461 |
   | Kafka (extended) | SASL PLAIN | BL-1462 |
   | XMPP (extended) | SASL PLAIN | BL-1463 |
   | MQTT 5 (extended) | CONNECT password, authentication data | BL-1464 |
   | HTTP-carried extended servers (S3, Azure Storage, Cloud Storage, OCI, MCP, SignalR, Socket.IO, gRPC, WebTransport, DNS over HTTPS) | `Authorization` and API-key headers | BL-1465 |

   The help text that states what is and is not withheld is BL-1436.
8. **Not secrets in clear, so not redacted.** SSH (logged below its own encryption, ciphertext only), SMB NTLM and
   HTTP Digest/NTLM/Negotiate (challenge responses, not the password; an NTLM or Negotiate token is replayable
   within its life but is not the password, so it is logged), POP3 APOP, CRAM-MD5, SCRAM and TLS-SRP (proofs), S3
   SigV4 signatures (derived, scoped and dated). Redacting a proof would hide what a conformance test needs to see.
   Data bodies (an uploaded file, a published message) are the client's payload, not a credential, and are not
   redacted.

## Alternatives considered

- **Redact in each sink** (`VerboseExchangeLog`, `TraceExchangeLog`): the sinks do not know the protocol and would
  need a switch over schemes, against "open for extension".
- **Redact on the event stream as well:** breaks tests that assert what a client sent, and gives Surl.Api nothing a
  log does not already.
- **Show the secret's length** (`PASS ******`): leaks a hint for no gain. Rejected.
- **An off switch for redaction:** the event stream covers the need. Rejected for now (decision 5).
- **Mirror curl and warn:** curl prints its own user's secret; a server's log holds everyone's. Rejected.

## Consequences

- BL-1418 and BL-1419 build the seam as decision 6; the filed tasks' `touches` match it, so none is amended.
- `Documentation/Planning/Decisions/README.md` lists this ADR.
- Eight extended-server tasks (BL-1458 to BL-1465) are filed behind BL-1419.

## Amendment 1 (BL-1419, decided by Claude under Stewart's delegation)

- `ServingEngine.OpenExchange` asks the exchange's server for a redactor once and puts a `RedactingExchangeLog` between
  `EventPublishingExchangeLog` and the level's log; `Flush` runs when the exchange ends, before `ExchangeEndedEvent`.
- **FTP data connections are logged raw.** Their bytes are the client's payload (decision 8), and a line redactor would
  rewrite file content that happens to look like a command. They still count in `ExchangeEndedEvent` and reach the event stream.
- **Decrypted-bytes notes** (QUIC, DTLS) are redacted per reported chunk by a fresh redactor from the same server, not by the
  exchange's wire redactor, because the wire bytes of those transports are ciphertext. Only received chunks are redacted. Events stay raw.
  Amendment 21 replaces "per reported chunk" for a stream's bytes when the redactor follows streams.

## Amendment 2 (BL-1425, decided by Claude under Stewart's delegation)

- The HTTP proxy's redactor is `HttpRequestHeadBytesRedactor` on curl's connection, the one the engine logs.
  Superseded by amendment 8; the rest of this amendment stands.
- **Origin-side bytes are not logged.** The proxy reaches origins through `IOriginConnector`; those connections never pass
  through the exchange log, so the `Authorization` the proxy forwards is withheld there by construction, and the one in
  curl's own head is masked on curl's side.
- **A `CONNECT` head is redacted like any head; tunnelled bytes after the `200` pass through the same redactor.** They are
  the client's own TLS (ciphertext, no field to match) or, in a test, plaintext HTTP, whose `Authorization` is masked too.
  A held partial line is written at `Flush`.

## Amendment 3 (BL-1423, decided by Claude under Stewart's delegation)

- **HTTP/2 form.** `Http2RequestBytesRedactor` keeps a copy of the connection's HPACK decoder, which decodes every client
  header block in order, so an indexed `authorization` is found as well as a literal, Huffman-coded or not. A block that
  carries `authorization` or `proxy-authorization` is logged with the payload of each of its `HEADERS` and `CONTINUATION`
  frames replaced by `****` (HPACK cannot show `Authorization: Basic ****` in place); the 9-byte frame headers are logged
  as received, so the dump's frame length fields name the original length and the dump's count is the new length (decision 2).
  Every other frame streams through as received, except as amendments 11 (`DATA`) and 15 (`PUSH_PROMISE` and broken
  header-block sequences) say.
- **Fail closed.** A block the copy cannot decode, one past 64 KiB, or one interrupted by another frame leaves the copy's
  table unknown; from then on every header-block payload on that connection is withheld. Amendment 15 adds four cases.
- **Switching.** The http server's redactor starts as the HTTP/1.x one and moves to the HTTP/2 one when the connection's
  first bytes are the client preface, or after the server sent `101` for `Upgrade: h2c`.

## Amendment 4 (BL-1424, decided by Claude under Stewart's delegation)

- **HTTP/3 form.** `Http3ProtocolServer` implements `IExchangeBytesRedactingServer`; its `Http3RequestBytesRedactor` serves the
  decrypted-bytes notes, each chunk by a fresh instance (Amendment 1). A `HEADERS` frame whose QPACK field section carries
  `authorization` or `proxy-authorization` is logged with its payload replaced by `****`, the frame type and length logged as
  received (as HTTP/2, Amendment 3). Surl advertises a QPACK table capacity of 0, so a fresh decoder reads any section.
- **Decoded header text** (`Decoded` layer, ADR-0169 decision 1) carries the same values in clear, so a chunk starting with `:` is read as
  `name: value` lines and those two values are written as `****`. Amendment 22 replaces how the text is told apart.
- **Fail closed, with one limit.** A `HEADERS` payload that does not decode or runs past its chunk is withheld. A chunk is read as
  frames from its first byte because the redactor is not told its stream; a body chunk starting mid-frame that happens to read as
  an undecodable `HEADERS` frame is withheld too, which loses log detail and never reveals a secret. Amendment 21 tells the
  redactor the stream and replaces this for a stream's bytes.

## Amendment 5 (BL-1610, decided by Claude under Stewart's delegation)

- **LDAP simple bind.** The BER length of the `[0]` simple password, and of a whole SASL `PLAIN` credentials field, is logged as
  `04` (the length of `****`), as decision 1 requires, so the field reads `80 04 ****`. The enclosing `BindRequest` and
  `LDAPMessage` lengths are logged as received (as HTTP/2, Amendment 3), because rewriting them would break the log's
  framing of the message; they still encode the password's length indirectly, which is the accepted limit. An empty
  password has nothing to withhold and is still logged as received (`80 00`).

## Amendment 6 (BL-1697, decided by Claude under Stewart's delegation)

A SOCKS tunnel carrying a text protocol logged its `PASS` and `Authorization` in clear, while the origin's own exchange
masked them (Surly bug report 2026-11-25, item 1). Amendment 2's rule for the HTTP proxy now holds for SOCKS too.

1. **Where the tunnel starts.** After the server sent a success reply (SOCKS5 `05 00 …`, longer than the two-byte method
   selection; SOCKS4 `00 5A …`), seen through `IReceivedBytesRedactor.ObserveSent`, the client's tunnelled bytes are
   redacted by a tunnel phase (`SocksTunnelBytesRedactor`). Before it, `Socks5PasswordRedactor` redacts the handshake.
   Replaced by amendment 12: the tunnel starts after the request's last byte, and `SocksHandshakeBytesRedactor`
   redacts the handshake.
2. **Text or binary.** SOCKS does not say what the tunnel carries, so the first tunnelled client byte decides: an ASCII
   letter (`A`-`Z`, `a`-`z`; amendment 9 adds the digits `1`-`9`) starts a text protocol's command or request line, and the rest of the tunnel goes through
   `LineBytesRedactor` with the SOCKS library's `SocksTunnelLineRedactionRule`; any other first byte (TLS `0x16`, MQTT,
   LDAP BER and other binary protocols) passes through unchanged, so ciphertext and binary frames are never held or
   mangled.
3. **What is masked.** Without regard to case: the argument of a `PASS ` or `ACCT ` command line (`PASS ****`), and the
   credentials of an `Authorization:` or `Proxy-Authorization:` header line, keeping the scheme word
   (`Authorization: Basic ****`), as decision 1 says. A folded continuation of such a header is masked too, and a bare
   line feed inside a held line ends a line, so a client sending `LF` alone has each line judged. Every other line is
   written as received. A body line that happens to match is masked too: that loses log detail and never reveals a
   secret.
4. **Its own rule class.** The SOCKS library may not reference `Surl.HttpMessage` (protocol servers reference only
   ADR-0002 decision 3's table), so the rule is a small class of its own, not `HttpRequestHeadLineRule`.
5. **Raw bytes elsewhere.** The event stream keeps raw bytes (decision 4); `SocksUpstreamTestCaseServer` keeps raw bytes.

Accepted limits:

- A binary protocol whose first byte is a letter (AMQP's `AMQP` header, SSH's banner) goes through the line redactor: its
  later bytes are held until a `CR LF`, and past 64 KiB written as `****` (decision 6). So is TLS started inside a text
  tunnel (`STARTTLS`). That loses log detail only.
- SASL and IMAP `LOGIN` credentials were not masked; amendment 9 masks them. The MQTT `CONNECT` password and the LDAP
  bind were not masked either; amendment 10 masks them and takes `0x10` and `0x30` out of point 2's pass-through.
- HTTP/2 in cleartext (`--http2-prior-knowledge`, or the frames after an `h2c` upgrade) carries `authorization` as an
  HPACK field, not a header line, so the line rule does not see it. Closed by amendment 7.
- A client that sends ahead of the server's replies (greeting and login together, or tunnelled bytes with the request),
  which curl does not do, has those early bytes logged as the handshake redactor writes them. Closed by
  amendment 12, with the limits it lists.

## Amendment 7 (BL-1709, decided by Claude under Stewart's delegation)

A SOCKS tunnel carrying HTTP/2 in cleartext logged its `authorization` and `proxy-authorization` values, because HPACK
carries them as fields no line rule sees.

1. **Finding HTTP/2.** A text tunnel (amendment 6, point 2) is redacted by `SocksTextTunnelBytesRedactor`, which looks
   for the 24-byte client preface (`PRI * HTTP/2.0 … SM …`) anywhere in the client's bytes, however they are split. It
   need not start a line: after an `h2c` upgrade it follows the upgrade request's body. Bytes up to and including the
   preface are logged by the line rule as before.
2. **Frames are read** when the preface is the tunnel's first bytes (prior knowledge), or the origin had sent bytes
   starting `HTTP/1.1 101` (the `h2c` upgrade). Everything after the preface then goes through `Surl.Http2`'s
   `Http2RequestBytesRedactor`, so the tunnel logs what the origin's own exchange logs (amendment 3): a header block
   with a credential has each frame's payload written as `****`, and every other frame as received, except as
   amendments 11 and 15 say.
3. **Otherwise every later byte is withheld**, written as one `****`. A preface that arrives late with no `101` seen
   (HTTP/2 with prior knowledge inside a `CONNECT` tunnel carried by the SOCKS tunnel, a `101` split across the
   origin's chunks, or a body that happens to hold the preface) is not proof that the following bytes are frames, and
   reading text as frames would log its secrets. This branch fails closed: it loses log detail and never reveals a
   secret.
4. **The reference.** `Surl.Protocol.Socks.UnitLibrary` now references `Surl.Http2.UnitLibrary`, one of ADR-0002
   decision 3's horizontal libraries any protocol server may reference, which adds to the references ADR-0121 lists
   for it. Amendment 6's point 4 was wrong to say the table bars `Surl.HttpMessage`; it lists it. The SOCKS line rule
   stays its own class because it also masks `PASS` and `ACCT`, which an HTTP head rule does not.
5. **Not decided here.** The event stream and `SocksUpstreamTestCaseServer` keep raw bytes (decision 4). A tunnel whose
   first byte is not a letter is still logged byte for byte, even if a preface appears in it.

Accepted limits:

- In a tunnel where the origin has sent bytes starting `HTTP/1.1 101`, whatever it upgraded to, a body that holds the
  preface is followed by bytes read as frames, so a credential in a later text request on that tunnel **can be logged**.
  curl sends nothing but HTTP/2 after a `101` to `Upgrade: h2c`, and does not send the preface after any other.
- Amendment 6's client that sends ahead can lose more than its early bytes: if the preface travels with the SOCKS
  request, the tunnel's first byte seen is a frame's `00`, so the whole tunnel is logged byte for byte. Closed by
  amendment 12.
- An over-long first `HEADERS` frame did not make `Http2RequestBytesRedactor` withhold the `CONTINUATION` after it,
  here or in the origin's own log. Closed by BL-1714: after a `HEADERS` or `CONTINUATION` frame past the 64 KiB cap,
  every later header-block payload on the connection is withheld.

## Amendment 8 (BL-1713, decided by Claude under Stewart's delegation)

A `CONNECT` tunnel through the HTTP proxy carrying HTTP/2 in cleartext could log its `authorization` and
`proxy-authorization` values, because amendment 2's redactor reads header lines only.

1. **Finding.** Composed `surl` never used the proxy's redactor: `ProxyRoutingProtocolServer.CreateRedactor` preferred
   the origin HTTP server's, so a change to the proxy's alone would not have reached `surl`. The router now prefers the
   proxy's. `ExchangeRedactionContext` does not say whether the listen URL is a proxy's, so with `--proxy` named the
   origin `http` and `https` listen URLs use the proxy's redactor too, in place of amendment 3's. Adding `IsProxy` to
   that contract was rejected: a contract change for no gain in what is withheld from a conforming client. This
   supersedes amendment 2's statement that the proxy's redactor is `HttpRequestHeadBytesRedactor`.
2. **Finding HTTP/2.** `HttpProxyConnectionBytesRedactor` redacts HTTP/1.x request heads as before until the 24-byte
   client preface, found however the chunks split it. Bytes up to and including the preface are logged by that rule.
   What follows depends on where the preface starts; no `101` is consulted.

   | Where the preface starts | What is logged after it |
   | --- | --- |
   | Where a request may start: ALPN `h2` on curl's connection, prior knowledge in a `CONNECT` tunnel, after an `h2c` upgrade request | What `Surl.Http2`'s `Http2RequestBytesRedactor` writes (amendment 3's form) |
   | Anywhere else: after the `PROXY` line of curl's `--haproxy-protocol` in a tunnel, mid-line, inside a request body | One `****` for every later byte (`HttpProxyWithheldBytesRedactor`): fail closed |

3. **Raw bytes elsewhere.** The event stream and `HttpProxyUpstreamTestCaseServer` keep raw bytes (decision 4).

Accepted limits:

- The `DATA` frames of an HTTP/2 `CONNECT` stream were still logged as received, so a credential tunnelled there could
  be logged: curl 8.21.0 `--proxy-http2 -p` sends plaintext HTTP/1.1 in them (fixture `https-proxy-h2-connect`).
  Closed by amendment 11.
- A preface where a request may start, followed by bytes that are not frames, is read as frames, so text sent after it
  **can be logged**, on an origin listen URL too when `--proxy` is named. curl does not do this.
- After a `101` to `Upgrade: h2c`, a client that sends anything but the exact preface and then `HEADERS` is not
  masked, although the origin HTTP server's redactor would have masked it. Only a non-conforming client does this.
- A preface anywhere else loses all later log detail, an upload body that holds it included, on origin listen URLs too
  when `--proxy` is named. Saying so in the help text is BL-1735.
- An upgrade request declaring `Content-Length: 0` leaves `HttpRequestHeadBytesRedactor` not at a request start until
  the next byte arrives, so the preface after it withholds the rest (BL-1736).
- FTP and mail logins in cleartext inside a `CONNECT` tunnel were still not masked. Closed by amendment 16.
- An over-long first `HEADERS` frame relied on BL-1714 to withhold the `CONTINUATION` after it, here as in the
  origin's own log. Closed by BL-1714, as under amendment 7.

## Amendment 9 (BL-1710, decided by Claude under Stewart's delegation)

SMTP, IMAP and POP3 logins carried through a SOCKS tunnel were logged in clear.

1. **Which tunnels are text.** A first tunnelled client byte that is an ASCII letter, or a digit from `1` to `9`
   (a numbered IMAP tag). `0` (`0x30`) is left out because it opens LDAP's BER sequence. This widens amendment 6,
   point 2, and amendment 7, point 5.
2. **Four rules, every line.** SOCKS does not say which protocol the tunnel carries, so `SocksTunnelLineRedactionRule`
   shows every client line to all of these and logs what the one that withholds from earliest in the line writes:

   | Rule | Masks |
   | --- | --- |
   | `SocksTunnelPasswordAndAuthorizationLineRule` | Amendment 6, point 3 |
   | `SocksTunnelImapLoginLineRule` | The IMAP `LOGIN` password (point 3) |
   | `SaslLineRedactionRule` (ADR-0183), `AUTH`, `334 ` | SMTP: the `AUTH` initial response and each line answering `334` |
   | `SaslLineRedactionRule`, `AUTH` and `AUTHENTICATE`, `+ ` | POP3 and IMAP: the initial response and each line answering `+` |

   `Surl.Protocol.Socks.UnitLibrary` now references `Surl.LineProtocol.UnitLibrary`, one of ADR-0002 decision 3's
   horizontal libraries, which adds to the references ADR-0121 lists for it.
3. **`LOGIN`.** On a line whose second word is `LOGIN`, the user name (atom or quoted string) stays and everything
   after it is `****`. A literal marker alone in the password's place is logged. When the line ends in `{n}` or
   `{n+}`, every line holding the literal's bytes, and the rest of the command on the line that finishes them, is
   `****`. The SOCKS library keeps its own rule: it may not reference the IMAP server (ADR-0002 decision 3), and
   masking everything after the user name suits a peer whose dialect is unknown.
4. **Continuations.** The origin's `334` or `+` line is recognised when the origin's reads split it, and when it has
   no space after it.
5. **Bare line feeds.** `SocksTunnelLinesRedactor` judges a line ended by a bare line feed as soon as it arrives, not
   at the next `CR LF`, so such a client's answers are judged while the continuation is still the origin's last line.
6. **Raw bytes elsewhere.** The event stream and `SocksUpstreamTestCaseServer` keep raw bytes (decision 4).

Accepted limits:

- Text that only looks like these forms (a message or HTTP body line, any line the origin starts with `+ `, the
  `+` that invites an IMAP `APPEND` literal) has a client line masked. That loses log detail only.
- The answer to a synchronizing literal is not awaited, so after a refused `LOGIN` literal the next lines are withheld
  until the announced count is used up.
- An initial response after two spaces is logged, as in the mail servers' own logs (BL-1738).
- An IMAP tag that begins the tunnel with neither a letter nor `1`-`9` leaves the tunnel's login unmasked. A tag
  beginning `0` is read as LDAP (amendment 10), whose rule logs such text as received.
- A client that answers before the continuation reaches it, which curl does not do, has that answer judged without it.
  So may a client so fast that its answer is judged between the relay's write of the continuation and its being shown
  to the redactor; that window has not been reproduced.

## Amendment 10 (BL-1706, decided by Claude under Stewart's delegation)

An MQTT `CONNECT` password and an LDAP simple bind password carried through a SOCKS tunnel were logged in clear.

1. **Two more first bytes.** A tunnel whose first client byte is `0x10` (an MQTT `CONNECT`) is redacted by
   `SocksTunnelMqttConnectRedactor`; `0x30` (the `SEQUENCE` every `LDAPMessage` is) by `SocksTunnelLdapBindRedactor`.
   This narrows amendment 6, point 2: TLS (`0x16`) and every other binary tunnel still pass through unchanged and
   unheld. The SOCKS library keeps its own small framing parsers: it may not reference the MQTT or LDAP servers
   (ADR-0002 decision 3).
2. **MQTT 3.1.1 and 5.** The first packet is held until whole and its password logged as `00 04 ****`, an empty one
   too. The Remaining Length is rewritten to count what is logged, so the log never shows the password's length, for
   3.1.1 as well (the MQTT server's own log keeps it as received). Later packets are logged as they arrive. A
   `CONNECT` whose flags announce a password that is not the packet's last field, whole, has everything after the keep
   alive logged as `****`. A packet declaring more than 64 KiB is one `****` and its bytes are dropped. A Remaining
   Length past four bytes is not MQTT: the tunnel is logged as received.
3. **LDAP.** Every `LDAPMessage` is framed. Each `BindRequest`'s authentication is logged as its tag, `04`, `****`,
   as amendment 5 writes a simple password; the enclosing lengths stay as received, and an empty password is logged as
   received. A SASL bind's whole credentials field is masked, mechanism name included: one rule, with nothing to miss.
   A bind that cannot be walked to its authentication, or is cut off, has everything after its operation's tag logged
   as `****`; one declaring more than 64 KiB is one `****` and its bytes are dropped. Every other message is logged as
   it arrives, held no further than its operation's tag, and from bytes that do not frame as a message on (the TLS
   records after `StartTLS`, a SASL security layer's buffers) everything is logged as received.
4. **Raw bytes elsewhere.** The event stream and `SocksUpstreamTestCaseServer` keep raw bytes (decision 4).

Accepted limits:

- MQTT 5 Authentication Data, a `CONNECT` property or in an `AUTH` packet, is logged as received (BL-1744).
- A packet or message declaring a length that ends before its secret leaves the rest logged as received, and so does
  a `CONNECT` whose protocol name length runs past the packet. Only a non-conforming client does this.
- An LDAP message length written in five or more octets, legal BER that curl does not send, leaves the tunnel logged
  as received from there on, as in the LDAP server's own log.
- The LDAP lengths enclosing a bind still encode the password's length indirectly, as in amendment 5.
- Another binary protocol starting `0x10` or `0x30` may have a packet held until whole, never more than 64 KiB, and
  lose log detail to `****`. That never reveals a secret.
- A client that sends ahead of the server's replies keeps amendment 6's limit. Closed by amendment 12.

## Amendment 11 (BL-1733, decided by Claude under Stewart's delegation)

An HTTP/2 `CONNECT` stream carries another protocol's bytes in its `DATA` frames, and `Http2RequestBytesRedactor`
logged them as received. curl 8.21.0 `--proxy-http2 -p` sends plaintext HTTP/1.1 there (fixture
`https-proxy-h2-connect`, recorded without `-u`), so an `Authorization` field sent there would have been logged (read
from the code, not recorded).

1. **Which streams.** The redactor keeps the ID of every stream on which a header block it decoded has `:method`
   `CONNECT`, whether the block is logged or withheld (one carrying `proxy-authorization` is withheld and still
   counts). A stream stays one for the rest of the connection. Extended `CONNECT` (RFC 8441) counts too.
2. **What is withheld.** A `DATA` frame on such a stream is logged as its 9-byte frame header, as received, then
   `****`; an empty one as its header alone. The same holds for a `DATA` frame on any stream once the HPACK decoder
   copy is lost (amendment 3, "fail closed"), because the redactor can then no longer tell which streams are tunnels.
   A `DATA` frame that interrupts an open header block loses the copy and is withheld.
3. **Whole payloads.** The content is not examined and no redactor is run per stream: nothing says what a tunnel
   carries, and a rule that guesses can miss. Rejected for that reason.
4. **Bounded memory.** At most 4,096 `CONNECT` streams are told apart one by one. A connection that opens more has
   every later `DATA` payload withheld.
5. **Everywhere the redactor is used.** The HTTP proxy, the origin HTTP servers, gRPC, DNS over HTTPS and the SOCKS
   tunnel share it, so the rule holds for every `CONNECT` stream whose frames they pass to it. It replaces "every
   other frame streams through as received" (amendment 3) and "every other frame as received" (amendment 7, point 2)
   for these frames.
6. **Raw bytes elsewhere.** The event stream keeps raw bytes (decision 4), so a test still sees what the client sent.

Accepted limits:

- A `DATA` frame sent on a stream before that stream's `CONNECT` header block, which a client may not do, is logged
  as received.
- A tunnel's payload is lost from the log even when it holds no secret (TLS ciphertext included). That loses log
  detail only.

## Amendment 12 (BL-1707, decided by Claude under Stewart's delegation)

A client that did not wait for the server's SOCKS replies had its RFC 1929 password logged in clear when it sent the
login with its greeting, and its tunnelled secrets when it sent them with its request. The server serves such a client
and forwards the early bytes to the origin. curl waits, so nothing changes for it.

1. **The client's bytes say where the tunnel starts.** `SocksHandshakeBytesRedactor` follows the handshake's own
   length fields (SOCKS4, SOCKS4a, the SOCKS5 greeting, the RFC 1929 or RFC 1961 messages and the request) to the
   request's last byte. Every byte after it is the tunnel's, whether or not a success reply has been sent. This
   replaces `Socks5PasswordRedactor` and widens amendment 6, point 1.
2. **No reply decides the handshake.** The byte after the greeting is `05` for a request. Otherwise the client sends
   RFC 1961 messages when the greeting offered method `01` and the listener has a keytab, because the server then
   selects `01`; otherwise an RFC 1929 login, when the byte is `01` or the greeting offered method `02`. A rule that
   waited for the method selection depended on how the client's bytes fell between reads: a login sent in a second
   read after a `05 00` was logged in clear. Rejected for that reason. A login is therefore masked whatever method
   the server selected, a refusal included.
3. **A success reply still starts the tunnel** when the handshake redactor has not found the request's end, as before.
4. **Replies are not the origin's.** The tunnel's redactor is shown what the server sends only after the success
   reply, so a method selection, login reply or success reply sent after early tunnel bytes is never read as part of
   a `334` or `+` continuation.
5. **Refused requests.** Bytes a client sent after a request the server then refuses are redacted as a tunnel's.
6. **Raw bytes elsewhere.** The event stream keeps raw bytes (decision 4).

Accepted limits:

- A line answering a SASL `334` or `+` that the client sends before the origin's continuation reaches it is judged
  without it (amendment 9), so it is logged as received.
- On a listener with a keytab, a client that offers method `01` and then sends an RFC 1929 login, which the server
  refuses, has it read as RFC 1961 messages and can have it logged as received.
- Bytes no SOCKS handshake explains (a first byte that is no SOCKS version, a request whose version or address type
  is wrong) are logged as received, with everything after them, unless a success reply follows. The server refuses
  each such request.

## Amendment 13 (BL-1717, decided by Claude under Stewart's delegation)

gRPC and DNS over HTTPS on HTTP/2 logged an API-key or session-token field in clear, though decision 7 says the
HTTP-carried extended servers withhold them: `Http2RequestBytesRedactor` named only `authorization` and
`proxy-authorization`.

1. **The caller names the further fields.** `Http2RequestBytesRedactor` always withholds
   `HttpWithheldHeaderNames.Credentials` and takes any further field names from whoever makes it, as the HTTP/1.x head
   redactor does. Names are matched without regard to case.
2. **gRPC and DNS over HTTPS** make it with `HttpWithheldHeaderNames.ApiKeysAndSessionTokens`. A header block carrying
   any field of `HttpWithheldHeaderNames.All` is logged as amendment 3 says: each of its `HEADERS` and `CONTINUATION`
   payloads is `****`, the 9-byte frame headers as received.
3. **No one else changes.** The http and https servers, the HTTP proxy and the SOCKS tunnel make it with no further
   names, so they withhold `authorization` and `proxy-authorization` only, as decision 7 and amendments 3, 7 and 8
   state. Rejected: withholding API keys in every HTTP/2 log, which would make the http server's HTTP/2 log differ from
   its HTTP/1.x one.
4. **Raw bytes elsewhere.** The event stream keeps raw bytes (decision 4).

Accepted limit:

- A gRPC or DNS over HTTPS origin reached in cleartext through the HTTP proxy or a SOCKS tunnel has its API-key and
  session-token fields logged in the tunnel's log, though the origin's own log withholds them. This narrows
  amendment 7, point 2 ("the tunnel logs what the origin's own exchange logs") to the two credential fields.

## Amendment 14 (BL-1718, decided by Claude under Stewart's delegation)

WebTransport logged an API-key or session-token field of an HTTP/3 request in clear, though decision 7 says the
HTTP-carried extended servers withhold them: `Http3RequestBytesRedactor` named only `authorization` and
`proxy-authorization`.

1. **The caller names the further fields**, as amendment 13 has it for HTTP/2. `Http3RequestBytesRedactor` always
   withholds `HttpWithheldHeaderNames.Credentials` and takes any further field names from whoever makes it. Names are
   matched without regard to case.
2. **WebTransport** makes it with `HttpWithheldHeaderNames.ApiKeysAndSessionTokens`. A `HEADERS` payload carrying any
   field of `HttpWithheldHeaderNames.All`, and that field's value in the decoded header text, are logged as `****`, as
   amendment 4 says for the two credential fields.
3. **The http and https servers** make it with no further names, so their HTTP/3 log withholds `authorization` and
   `proxy-authorization` only, as decision 7 and amendment 4 state. Rejected: withholding API keys there too, which
   would make their HTTP/3 log differ from their HTTP/1.x and HTTP/2 ones.
4. **Raw bytes elsewhere.** The event stream keeps raw bytes (decision 4).

Accepted limit, closed by amendment 21:

- Each chunk is redacted on its own (amendment 1), so when QUIC splits a `HEADERS` frame, the chunks after the first are
  logged as received unless they happen to read as a `HEADERS` frame, and a secret in them **can be logged**. This
  narrows amendment 4's "never reveals a secret" to a frame that arrives in one chunk.

## Amendment 15 (BL-1728, decided by Claude under Stewart's delegation)

Amendment 3's "Fail closed" bullet did not cover a header-block frame that arrives out of sequence, or a `PUSH_PROMISE`
from the client. `Http2RequestBytesRedactor` now treats each as leaving its HPACK decoder copy's table unknown.

1. **The four cases.**

   | Case | RFC 9113 |
   | --- | --- |
   | A `HEADERS` frame inside an open header block | section 6.10 |
   | A `CONTINUATION` frame with no open header block | section 6.10 |
   | A `CONTINUATION` frame on another stream than its block's | section 6.10 |
   | A `PUSH_PROMISE` frame from the client, inside a block or not | section 8.4 |

2. **What is logged.** The frames held for an open block, the offending frame's payload and every later header-block
   payload on that connection (`HEADERS`, `CONTINUATION` and `PUSH_PROMISE`) are logged as `****`, the 9-byte frame
   headers as received. As amendment 11, point 2 says for a lost decoder copy, every later `DATA` payload is withheld
   too.
3. **Everywhere the redactor is used** (amendment 11, point 5). This replaces "every other frame streams through as
   received" (amendment 3) and "every other frame as received" (amendment 7, point 2) for these frames.
4. **Raw bytes elsewhere.** The event stream keeps raw bytes (decision 4).

Accepted limit:

- After any of the four, the connection's log loses the payload of every later header-block and `DATA` frame, secret or
  not. That loses log detail only.

## Amendment 16 (BL-1734, decided by Claude under Stewart's delegation)

FTP, SMTP, IMAP and POP3 logins sent through an HTTP proxy `CONNECT` tunnel were logged in clear, because the proxy
judged tunnelled lines only as HTTP head lines.

1. **Where the tunnel's text starts.** Before any HTTP/2 preface, `HttpProxyRequestOrTunnelBytesRedactor` decides on
   these lines. Each is held until its line feed, or past 64 KiB, which makes it the tunnel's. From the first line
   that is the tunnel's, every later byte of the connection is redacted line by line.

   | Line | The tunnel's when |
   | --- | --- |
   | One beginning where a request may start (a `CONNECT` head, any other head or a body has just ended) with an ASCII letter, a digit from `1` to `9` (amendment 9, point 1) or a line feed | It is not a request line: three words, one space between them, the last starting `HTTP/` or `RTSP/`, ended by CR LF |
   | A later line of a `CONNECT` head | It does not end with CR LF |
   | The first line after a `CONNECT` head that declares a body | Always: a `CONNECT` has none |

   The last two rows and the bare line feed are there because the proxy accepts a head whose lines end with a bare
   line feed, and the head redactor does not see such a head end.
2. **The client's bytes decide.** The proxy's `200` is not waited for, so tunnelled bytes sent with the `CONNECT` head
   are masked too, and a `CONNECT` retried after a `407` is still read as a head. Rejected: switching when
   `ObserveSent` shows the `200`, which would log what a client sends before it. `ObserveSent` still shows the rules
   the continuation prompts.
3. **What is masked.** Each line is shown to four rules, and what the one that withholds from earliest in it writes is
   logged:

   | Rule | Masks |
   | --- | --- |
   | `HttpProxyTunnelPasswordAndCredentialHeaderLineRule` | The argument of `PASS` and `ACCT`; the credentials of an `Authorization` or `Proxy-Authorization` header line and its folded lines, after the scheme word |
   | `HttpProxyTunnelImapLoginLineRule` | Everything after the user name of an IMAP `LOGIN`, and the lines of its literals; words are separated by spaces and tabs |
   | `SaslLineRedactionRule` (ADR-0183), `AUTH`, `334 ` | SMTP: the `AUTH` initial response and each line answering `334` |
   | `SaslLineRedactionRule`, `AUTH` and `AUTHENTICATE`, `+ ` | POP3 and IMAP: the initial response and each line answering `+` |

   A line ended by a bare line feed is judged as soon as it ends. A `334` or `+` line split between the origin's reads,
   or sent with no space after it, is a continuation (amendment 9, points 4 and 5).
4. **Its own classes.** The rules the SOCKS library holds are written again in the proxy library, because protocol
   libraries never reference each other (amendment 20 moves both into `Surl.LineProtocol`). The proxy library now references
   `Surl.LineProtocol.UnitLibrary`, one of ADR-0002 decision 3's horizontal libraries, which adds to the references
   ADR-0088 lists for it.
5. **Unchanged.** Forwarded requests, and HTTP or RTSP carried in a tunnel, whose lines point 1 does not give to the
   tunnel, are redacted by `HttpRequestHeadBytesRedactor` as before, bodies logged as received. A tunnel starting with any other byte (TLS and
   other binary protocols) is too. The preface handling of amendment 8 stays: tunnelled text is not a place where a
   request may start, so a preface after it withholds every later byte. With `--proxy` named, origin `http` and `https`
   listen URLs use this redactor (amendment 8, point 1), so all of this holds for them.
6. **Raw bytes elsewhere.** The event stream and `HttpProxyUpstreamTestCaseServer` keep raw bytes (decision 4).

Not masked inside a `CONNECT` tunnel: the MQTT `CONNECT` password and the LDAP bind (BL-1765; closed by amendment
23); the logins of every other protocol, the extended servers' included; and, in tunnelled text, any header but the two
credential fields of `HttpWithheldHeaderNames.Credentials` (BL-1759 adds the API-key fields to the proxy).

Accepted limits:

- A tunnel whose first byte is not a letter, `1`-`9` or a line feed (an IMAP tag beginning `0` or a punctuation mark,
  a line beginning with a space) may have no login masked.
- A first tunnelled line of three words whose last starts `HTTP/` or `RTSP/`, ended by CR LF, is read as a request
  line, so the tunnel is read as HTTP. No FTP or mail client opens that way.
- A request head on the proxy connection, other than a `CONNECT` head, whose later lines end with a bare line feed is
  judged by the head redactor as before this amendment.
- Text that only looks like these forms (a message line, an HTTP body line after a `PROXY` line sent into the tunnel,
  a line after a request line that is not three words or is ended by a bare line feed) has a secret-shaped part masked.
  That loses log detail only.
- Amendment 9's limits hold here too: the answer to a synchronizing literal is not awaited; a SASL answer sent before
  the continuation reaches the client is judged without it; and so may one from a client so fast that its answer is
  judged between the proxy's write of the continuation and its being shown to the redactor (not reproduced).

## Amendment 17 (BL-1758, decided by Claude under Stewart's delegation)

`Http2RequestBytesRedactor` wrote the first 24 bytes as the client preface without comparing them and read the rest as
frames, so an HTTP/1.1 request sent to a `grpc` listen URL, or sent to an `http` listen URL after its `101` to
`Upgrade: h2c`, was logged with its credentials, API keys and session tokens.

1. **The preface is compared.** Each byte is compared with the 24-byte preface as it arrives, however the bytes are
   split. The bytes that match are logged as received.
2. **The first byte that differs fails closed.** One `****` is written in its place and nothing more: not that byte, no
   later byte, and nothing from `Flush`. So an HTTP/1.1 `GET` is logged as `****`, and a `PUT`, which shares the
   preface's `P`, as `P****`.
3. **Everywhere the redactor is used** (amendment 11, point 5). gRPC and the http server after an `h2c` upgrade gain the
   rule. DNS over HTTPS, the HTTP proxy and the SOCKS tunnel only ever give the redactor bytes that open with the exact
   preface, so their logs do not change.
4. **Rejected:** redacting such bytes as HTTP/1.x request heads in the gRPC server. It needs a new project reference
   there, leaves the `101` case open, and keeps log detail only for a client that does not speak the listener's protocol.
5. **Raw bytes elsewhere.** The event stream keeps raw bytes (decision 4), so a test still sees what the client sent.

Accepted limits:

- When the bytes the HTTP/2 redactor receives do not open with the preface (a `grpc` connection, or an `http` one after
  its `101`), everything after the matching bytes is lost from the log, secret or not. That loses log detail only.
- On an `http` listen URL, HTTP/2 bytes a client sends after its `h2c` upgrade request without waiting for the `101`,
  which curl does not do, are still read by the HTTP/1.x redactor, which sees no HPACK field, so a secret in them
  **can be logged**. Bytes that arrive after the redactor moves to HTTP/2 and do not start with the preface are
  withheld.

## Amendment 18 (BL-1759, decided by Claude under Stewart's delegation)

An origin reached in cleartext through the HTTP proxy had its API-key and session-token fields logged in clear in the
proxy's log, though the origin's own log withholds them for the HTTP-carried extended servers (amendment 13's accepted
limit).

1. **The proxy withholds them wherever it withholds the credential fields**: in forwarded requests and `CONNECT`
   tunnels alike, the `CONNECT` head included, because it cannot tell which origin it carries. The names are read from
   `HttpWithheldHeaderNames.ApiKeysAndSessionTokens` and matched without regard to case. The accepted limits below say
   where they can still be logged.

   | How the proxy reads the bytes | What is logged for such a field |
   | --- | --- |
   | An HTTP/1.x request head (`HttpRequestHeadBytesRedactor`) | The whole value, and each folded line of it, as `****` |
   | Tunnelled text (amendment 16, point 1): `HttpProxyTunnelPasswordAndCredentialHeaderLineRule` | The same, no scheme word kept |
   | HTTP/2 after a preface where a request may start (`Http2RequestBytesRedactor`) | Each `HEADERS` and `CONTINUATION` payload of a header block carrying it, as `****` (amendment 3's form) |

2. **Origin listen URLs.** With `--proxy` named, origin `http` and `https` listen URLs use this redactor (amendment 8,
   point 1), so their logs withhold the fields too. That withholds more, never less.
3. **What this replaces.** The HTTP proxy's part of amendment 13, point 3, and of its accepted limit; and, for these
   five fields, amendment 16's "in tunnelled text, any header but the two credential fields". The SOCKS tunnel is
   unchanged (BL-1760), and so are the http and https servers' own redactors.
4. **Raw bytes elsewhere.** The event stream keeps raw bytes (decision 4), so a test still sees what the client sent.

Accepted limits:

- A line of a tunnelled text protocol that looks like one of these headers has its value masked, and so does a body
  line judged as text. That loses log detail only.
- Amendment 16's limits on where tunnelled bytes are read as HTTP or text hold for these fields as for the credential
  fields.
- Three shapes curl does not send, where one of these fields, or a credential field, **can be logged**. Each predates
  this amendment for the credential fields.

  | Shape | What is logged | Closed by |
  | --- | --- | --- |
  | A request head whose request line ends with CR LF and whose later lines end with a bare line feed, which the proxy accepts: forwarded, a `CONNECT` head's request line aside, or HTTP carried in a tunnel | The lines up to the next CR LF are judged as one, by the first one's name | BL-1771 |
  | A `CONNECT` head holding a secret field followed by a folded line that ends with a bare line feed | The folded line, as received | Not planned: the proxy refuses folded lines |
  | A field in the trailers of a chunked request body | The trailer line, as received (the HTTP/1.x head redactor, so on every HTTP server too) | BL-1770 |

## Amendment 19 (BL-1760, decided by Claude under Stewart's delegation)

An origin reached in cleartext through a SOCKS tunnel had its API-key and session-token fields logged in clear in the
tunnel's log, though the origin's own log withholds them for the HTTP-carried extended servers (amendment 13's accepted
limit).

1. **The SOCKS tunnel withholds them in every text tunnel**, because SOCKS does not say what the tunnel carries. The
   names are read from `HttpWithheldHeaderNames.ApiKeysAndSessionTokens`, and the credential fields' names from
   `HttpWithheldHeaderNames.Credentials`; both are matched without regard to case.

   | How the tunnel reads the bytes | What is logged for such a field |
   | --- | --- |
   | Text lines: `SocksTunnelPasswordAndCredentialHeaderLineRule` | The whole value, and each folded line of it, as `****`; no scheme word is kept |
   | HTTP/2 after a preface the frames are read after (`Http2RequestBytesRedactor`; amendment 7) | Each `HEADERS` and `CONTINUATION` payload of a header block carrying it, as `****` (amendment 3's form) |

2. **The rule is renamed.** `SocksTunnelPasswordAndAuthorizationLineRule` (amendments 6 and 9) is now
   `SocksTunnelPasswordAndCredentialHeaderLineRule`, because it masks more than the authorization headers.
3. **What this replaces.** The SOCKS tunnel's part of amendment 13, point 3, and of its accepted limit, and
   amendment 18, point 3's "the SOCKS tunnel is unchanged". The tunnel's log now withholds at least what the origin's
   own log does of these fields; for an `http` or `https` origin, whose own redactors name no API-key field, it
   withholds more, never less (narrowing amendment 7, point 2).
4. **Raw bytes elsewhere.** The event stream keeps raw bytes (decision 4), so a test still sees what the client sent.

Accepted limits:

- A line of another protocol that looks like one of these headers has its value masked, and so does a body line. That
  loses log detail only.
- The limits on where a tunnel is read as text or as HTTP/2 (amendments 6, 7, 9 and 10), and on bytes no SOCKS
  handshake explains (amendment 12), hold for these fields as for the credential fields.
- Three shapes curl does not send, where one of these fields, or a credential field, **can be logged**. Each predates
  this amendment for the credential fields.

  | Shape | What is logged |
  | --- | --- |
  | A folded line following a header line longer than 64 KiB, which is logged as `****` without the rule seeing it | The folded line, as received |
  | A header line that starts with a space or a tab and follows no withheld header | The line, as received |
  | A header that follows a bare carriage return | The whole is judged as one line, by the first name in it |

## Amendment 20 (BL-1767, decided by Claude under Stewart's delegation)

The SOCKS server and the HTTP proxy each held its own copy of the tunnel line rules (amendment 16, point 4), so a fix to
one could miss the other.

1. **One copy, in `Surl.LineProtocol`.** Both already reference it (amendments 9 and 16), and it references
   `Surl.Protocol.Abstractions`, where `HttpWithheldHeaderNames`, `LineBytesRedactor` and `ILineRedactionRule` are, so
   no new library or reference is needed. Rejected: the abstractions library, because the combining rule needs
   `SaslLineRedactionRule`, which is in `Surl.LineProtocol`. The four classes are public:

   | Shared class | Replaces, in SOCKS and in the HTTP proxy |
   | --- | --- |
   | `TunnelLinesRedactor` | `SocksTunnelLinesRedactor`, `HttpProxyTunnelLinesRedactor` |
   | `TunnelLineRedactionRule` | `SocksTunnelLineRedactionRule`, `HttpProxyTunnelLineRedactionRule` |
   | `TunnelPasswordAndCredentialHeaderLineRule` | `SocksTunnelPasswordAndCredentialHeaderLineRule`, `HttpProxyTunnelPasswordAndCredentialHeaderLineRule` |
   | `TunnelImapLoginLineRule` | `SocksTunnelImapLoginLineRule`, `HttpProxyTunnelImapLoginLineRule` |

   Where amendments 6, 9, 16, 18 and 19 name a replaced class, the shared one is meant. This replaces amendment 6,
   point 4, amendment 9, point 3's "the SOCKS library keeps its own rule", and amendment 16, point 4's "its own
   classes".
2. **`LOGIN` words are separated by spaces and tabs.** The copies differed in this: the proxy's rule did so, the SOCKS
   one split on spaces only. A tab-separated `LOGIN` line through a SOCKS tunnel now has its password logged as `****`.
   Splitting on tabs alone would have shown passwords the SOCKS rule masked (a tab inside or before the tag, or
   standing as the user name), so the shared rule reads each line both ways: words separated by spaces and tabs, and by
   spaces only. A line either reading takes for `LOGIN` is one, it is masked from the earlier password of the two, and a
   literal it announces is withheld. Blanks after the user name are logged either way. Both servers therefore mask at
   least the password either copy masked.
3. **What stays apart.** Where each server starts reading a connection as tunnelled text, and what it does at an HTTP/2
   preface, differ and stay in each server's library.
4. **Raw bytes elsewhere.** The event stream keeps raw bytes (decision 4), so a test still sees what the client sent.

Accepted limits:

- A line neither reading takes for `LOGIN` is logged as received, one with no tag included (BL-1750).
- The limits amendments 9, 16, 18 and 19 give for these rules hold for both servers alike.

## Amendment 21 (BL-1762, decided by Claude under Stewart's delegation)

QUIC reports each in-order piece of a stream as its own chunk, and each chunk was redacted on its own, so the later
chunks of a split HTTP/3 `HEADERS` frame were logged as received, a secret included (amendment 14's accepted limit,
read from the code, not recorded).

1. **The redactor follows the stream.** `Surl.Protocol.Abstractions` gains `IStreamReceivedBytesRedactor`, an
   `IReceivedBytesRedactor` with `RedactStream(streamId, received, output)`, which holds nothing back. When the
   server's redactor is one, the engine keeps one instance per exchange and gives it every received `Decrypted` chunk
   that has a stream ID, one call at a time. DTLS plaintext, QUIC datagrams and `Decoded` text have no stream bytes to
   follow and are redacted alone, each by a fresh redactor, as amendment 1 says; so are the chunks of a server whose
   redactor does not follow streams.
2. **A frame payload is not the stream's next bytes.** HTTP/3 reports a `DATA` payload a second time, as `Decrypted`
   bytes of the same stream (ADR-0169 decision 1). `IDecryptedBytesSink` gains `ReceivedFramePayload`, whose default
   calls `Received`; `Surl.Http3` reports `DATA` payloads through it, and the engine redacts each alone, as before. The
   published event is unchanged.
3. **HTTP/3 form.** A client's bidirectional stream is read as frames from its first byte, however it is split. A frame
   header is logged as received as its bytes arrive. A `HEADERS` payload is logged as received only when it is whole in
   the chunk that completes its frame header, decodes and carries no secret field; otherwise it is `****` once, and every
   further byte of it is left out. Every other frame's payload is logged as received, so a `DATA` payload is no longer
   mistaken for a frame. A unidirectional stream carries no request, and each of its chunks is redacted alone.
4. **Nothing is held back.** Holding a split payload until it is whole could show a non-secret one, but needs a buffer of
   up to 64 KiB per stream and a flush when the stream ends, which nothing reports. Rejected: a split `HEADERS` payload
   is withheld whether or not it carries a secret, as its first chunk already was (amendment 4).
5. **Bounded memory.** Only streams part-way through a frame are remembered, at most 4,096. When one more would be, the
   redactor has lost a stream's place, and every later bidirectional stream chunk is `****` (fail closed, as amendment
   11, point 4).
6. **Raw bytes elsewhere.** The event stream keeps raw bytes (decision 4).

Accepted limits:

- A WebTransport bidirectional stream (signal `0x41`, session ID, then data) is read as frames too, so data that reads
  as a `HEADERS` frame is withheld, now possibly across several chunks. That loses log detail only (BL-1775).
- A stream reset or finished part-way through a frame stays remembered until the connection ends, and counts towards the
  4,096 (BL-1775).
- `Decoded` header text is still redacted only when it starts with `:` and line by line, so a malformed request whose
  first field is a regular one, or whose secret value holds CR LF, **can be logged** in that note. This amendment changes
  nothing there (BL-1773). Closed by amendment 22.

## Amendment 22 (BL-1773, decided by Claude under Stewart's delegation)

The HTTP/3 server reports a request's decoded header list before it validates it, so a malformed request could put a
secret in the `Decoded` note two ways (read from the code, then reproduced by unit tests, not recorded on the wire): a
list whose first field is a regular one was read as HTTP/3 frames, and a value holding CR LF put its rest on a line of
its own. Both were logged as received.

1. **The engine says which text it is.** `Surl.Protocol.Abstractions` gains `IDecodedTextReceivedBytesRedactor`, an
   `IReceivedBytesRedactor` with `RedactDecodedText(decodedText, output)`, which holds nothing back. The engine gives
   every received `Decoded` chunk to a fresh redactor's `RedactDecodedText` when the redactor is one, and to `Redact`
   when it is not. `Http3RequestBytesRedactor` is one, and no longer guesses from a leading `:`: its `Redact` reads
   frames only. This replaces amendment 4's "a chunk starting with `:`".
2. **One header, one line.** The decoded header text writes a CR inside a name or value as the two characters `\r`, and
   an LF as `\n`, in the note and in the published `ExchangeBytesEvent` alike, so the whole of a secret field's value is
   on its line and is written as `****`. RFC 9114 makes a request carrying either malformed, so a well-formed
   request's text is unchanged, and ADR-0169 decision 1's one line per header now holds for every list. A backslash is written as it is,
   so the text does not say whether `\r` stood in the value as two characters. This is not a change to wire bytes: the
   `Decrypted` and wire layers keep what the client sent (decision 4).
3. **Rejected.** Reporting the list only after validation hides what a malformed request carried from a test. Withholding
   every line after a secret one hides the other fields of well-formed requests. Passing the redactor the field list
   instead of text changes `IDecryptedBytesSink` for one reader.

Accepted limit:

- A secret field is matched by its exact name, without regard to case, in the note as in the `HEADERS` payload
  (amendment 4). A malformed name that only resembles one, such as `authorization` followed by a space or a CR, is
  another name, and its value **can be logged**.

## Amendment 23 (BL-1765, decided by Claude under Stewart's delegation)

An MQTT `CONNECT` password and an LDAP simple bind password sent through an HTTP proxy `CONNECT` tunnel were logged in
clear: the proxy gave a tunnel starting `0x10` or `0x30` to the request head redactor, which masks header fields only.

1. **One copy, in `Surl.LineProtocol`.** SOCKS's redactors move there, public, as amendment 20 did the line rules. Both
   servers already reference it, so no new library or reference is needed. Rejected: copies in the proxy library, which a
   fix could miss; and a new library, for two classes that sit beside the rules both servers already share.

   | Shared class | Was |
   | --- | --- |
   | `TunnelMqttConnectRedactor` (with `TunnelMqttConnectPasswordFinder`) | `SocksTunnelMqttConnectRedactor`, `SocksMqttConnectPasswordFinder` |
   | `TunnelLdapBindRedactor` (with `TunnelBerHeader`, `TunnelBerRead`) | `SocksTunnelLdapBindRedactor`, `SocksBerHeader`, `SocksBerRead` |
   | `TunnelBinaryLoginRedactors`, which chooses by the first byte | `SocksTunnelBytesRedactor`'s own choice |

   Where amendment 10 names a replaced class, the shared one is meant, and this replaces its point 1's "the SOCKS
   library keeps its own small framing parsers". What each redactor writes, and amendment 10's limits, are unchanged and
   hold for both servers.
2. **Where the proxy's tunnel starts.** At the byte after the empty line that ends a `CONNECT` head, whether or not the
   head declares a body. `ConnectHeadEndFinder` finds it from the lines alone: a head starts at a line holding
   `CONNECT ` whose last word starts `HTTP/`, and lines end with a line feed, with or without a carriage return. When
   that byte is `0x10` or `0x30`, every later byte of the connection is that redactor's. The proxy's `200` is not waited
   for (amendment 16, point 2). The finder is apart from the line rules' own reading, so it also finds a head after a
   leading empty line, one a request body runs into in the same read, and one retried after a `407`, however the bytes
   are split. Rejected: relying on where the head redactor sees a request start, which each of those three defeats.
3. **A `PROXY` line first.** curl 8.21.0 with `--haproxy-protocol -p` sends a PROXY protocol line in the tunnel before
   the MQTT `CONNECT` (fixture `proxytunnel-haproxy-mqtt-user-and-password`). So the proxy judges the byte after a first
   tunnel line starting `PROXY ` the same way.
4. **What this replaces.** Amendment 16's "not masked inside a `CONNECT` tunnel" for MQTT and LDAP; for these two first
   bytes, its point 1's "every later byte of the connection is redacted line by line" and its point 5's "a tunnel
   starting with any other byte (TLS and other binary protocols) is too". Every other binary tunnel is redacted as
   before.
5. **Raw bytes elsewhere.** The event stream and `HttpProxyUpstreamTestCaseServer` keep raw bytes (decision 4).

Evidence: pinned upstream curl 8.21.0 sent the MQTT 3.1.1 `CONNECT` straight after the proxy's `200` (fixture
`proxytunnel-mqtt-user-and-password`). The LDAP test bytes are the bind it sent straight to an LDAP server
(`Surl.Protocol.Ldap.UnitTests/Fixtures/simple-bind-base-search`); no LDAP exchange through a tunnel was recorded.

Accepted limits:

- A tunnel starting `0x10` or `0x30` no longer has its later lines judged by any line rule, so a secret in such a line
  (a header field, or, where the tunnel was read as text, a `PASS`, `LOGIN` or SASL line) **can be logged**. Where it
  was read as text this includes an IMAP tag beginning `0` (after a `PROXY` line, or a `CONNECT` head that declares a
  body or ends a line with a bare line feed), which amendments 9 and 16 had masked or left as here.
- Any client line holding `CONNECT ` whose last word starts `HTTP/` (another method's request line ending in
  `CONNECT`, a header field or a body line as much as a `CONNECT` request line) starts a head for the finder. A `0x10`
  or `0x30` after the next empty line then hands the rest of the connection to that redactor, so later header fields,
  and the password of a tunnel opened later on that connection, **can be logged**. Only a client crafting its own bytes
  does this. Rejected: matching only a line that begins `CONNECT `, which a body running into the request line defeats.
- A login that does not start at either of the two places (behind a PROXY protocol version 2 header, or other bytes) is
  not masked by these two redactors.
- A SOCKS tunnel that starts with a `PROXY` line is read as text, so an MQTT or LDAP password after it **can be logged**
  (BL-1780).
