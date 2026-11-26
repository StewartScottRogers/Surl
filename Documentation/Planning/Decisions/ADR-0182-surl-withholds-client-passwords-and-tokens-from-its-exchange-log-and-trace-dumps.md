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
  Every other frame streams through as received.
- **Fail closed.** A block the copy cannot decode, one past 64 KiB, or one interrupted by another frame leaves the copy's
  table unknown; from then on every header-block payload on that connection is withheld.
- **Switching.** The http server's redactor starts as the HTTP/1.x one and moves to the HTTP/2 one when the connection's
  first bytes are the client preface, or after the server sent `101` for `Upgrade: h2c`.

## Amendment 4 (BL-1424, decided by Claude under Stewart's delegation)

- **HTTP/3 form.** `Http3ProtocolServer` implements `IExchangeBytesRedactingServer`; its `Http3RequestBytesRedactor` serves the
  decrypted-bytes notes, each chunk by a fresh instance (Amendment 1). A `HEADERS` frame whose QPACK field section carries
  `authorization` or `proxy-authorization` is logged with its payload replaced by `****`, the frame type and length logged as
  received (as HTTP/2, Amendment 3). Surl advertises a QPACK table capacity of 0, so a fresh decoder reads any section.
- **Decoded header text** (`Decoded` layer, ADR-0169 decision 1) carries the same values in clear, so a chunk starting with `:` is read as
  `name: value` lines and those two values are written as `****`.
- **Fail closed, with one limit.** A `HEADERS` payload that does not decode or runs past its chunk is withheld. A chunk is read as
  frames from its first byte because the redactor is not told its stream; a body chunk starting mid-frame that happens to read as
  an undecodable `HEADERS` frame is withheld too, which loses log detail and never reveals a secret.

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
  which curl does not do, has those early bytes logged as the handshake redactor writes them.

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
   with a credential has each frame's payload written as `****`, and every other frame as received.
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
  request, the tunnel's first byte seen is a frame's `00`, so the whole tunnel is logged byte for byte (BL-1707).
- An over-long first `HEADERS` frame does not yet make `Http2RequestBytesRedactor` withhold the `CONTINUATION` after
  it, here or in the origin's own log (BL-1714).

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
- FTP and mail logins in cleartext inside a `CONNECT` tunnel are still not masked, as before this amendment (BL-1734).
- An over-long first `HEADERS` frame relies on BL-1714 to withhold the `CONTINUATION` after it, here as in the
  origin's own log.

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
- A client that sends ahead of the server's replies keeps amendment 6's limit (BL-1707).

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
