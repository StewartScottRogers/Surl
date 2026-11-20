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
