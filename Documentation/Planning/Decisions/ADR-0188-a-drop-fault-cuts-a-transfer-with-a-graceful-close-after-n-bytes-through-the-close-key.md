# ADR-0188 — A drop fault cuts a transfer with a graceful close after N bytes through the `close` key

- **Status:** Accepted
- **Date:** 2026-11-22
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-22, in BL-1608.
- **Amends:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decision 4 (the fault model: a new key on `drop`; the
  kind table and the closed set of kinds are unchanged) and its 2026-11-21 amendment (BL-1518).
- **Builds on:** [ADR-0003](ADR-0003-upstream-curl-is-surls-only-oracle.md),
  [ADR-0021](ADR-0021-a-lingering-close-for-every-tcp-connection.md) (the lingering close),
  [ADR-0141](ADR-0141-how-a-server-applies-a-command-stage-delay-or-drop.md) (command-stage drop).

## Context

A test of `curl -C -` (resume) or of a client's handling of a short download needs a server that sends exactly N
bytes and then ends the connection *politely*. `drop;after-bytes=N` cuts at N but then resets
(`IConnection.Abort`, `StreamConnection.Abort` -> `ResetAndClose`), so the client loses bytes it had not read yet and
upstream curl reports a receive error. The Surly cookbook's report of 2026-11-22 (item 1, a question) wanted a cut-off
download that keeps exactly 4,000,000 bytes. The report's client was the Curl port, so the report is a lead and never
evidence (ADR-0003).

Upstream curl's documented meanings (https://curl.se/libcurl/c/libcurl-errors.html, checked 2026-11-22 against the error
set of the reference release, curl 8.21.0):

- `CURLE_PARTIAL_FILE (18)`: "A file transfer was shorter or larger than expected. This happens when the server first
  reports an expected transfer size, and then delivers data that does not match the previously given size."
- `CURLE_RECV_ERROR (56)`: "Failure with receiving network data."

So exit 18 needs a declared size (HTTP `Content-Length`, FTP's `150 ... (n bytes)` or `SIZE`) followed by an orderly
close before that size is reached; exit 56 is a connection that failed. This ADR states the documented meaning only. The
exit code for each protocol is **measured** against pinned upstream curl 8.21.0 in BL-1618, never assumed here, and no
Curl port output is cited.

## Decision

### 1. The spelling: a key `close` on `drop`, values `reset` (default) and `graceful`

`--fault drop;after-bytes=4000000;close=graceful`. Of the options weighed:

1. **A key on `drop`** (chosen). `close=reset|graceful`, default `reset`. A key is additive: `FaultKind` stays closed
   (decision 4 says a new kind needs an ADR, and a kind would also touch every table that lists kinds), the key table
   gains one row, and an absent key is today's behaviour byte for byte.
2. A new kind `cut` that requires `after-bytes`. Rejected: it duplicates `drop`'s scope, stage and `times` rules in
   every host and in the kind table, for what is one property of how the drop ends.
3. Making `drop` graceful, or `error` answer a cut. Rejected: every existing `drop` rule still resets (ADR-0124 decision
   4, its BL-1518 amendment, "a drop is a connection that vanishes"), and "configuration only grows".

**Where it is allowed.** Only on `drop` with `after-bytes` (value above 0). A graceful close before any byte has no
meaning a test can tell from a refused connection, and a command-stage drop (ADR-0141) answers nothing by design.
`FaultRule` gains `Close` (`FaultClose.Reset` default, or `Graceful`); every existing rule reads as `Reset`.
**Every existing `drop` rule still resets.**

### 2. What is sent and closed when the count reaches N

The write that reaches the count is cut at N, **written and flushed through to the transport**, and only then is the
close made. The count and its lock are ADR-0124's BL-1518 amendment unchanged: one count over every byte the exchange
sends on all its connections. A graceful close never uses `Abort`; it uses the connection's `DisposeAsync` path, which
is ADR-0021's lingering close (flush, FIN, then read and discard what the client still sends until it half-closes or
`LingeringCloseTime`), so no reset is ever sent for unread client bytes and the client reads exactly N bytes before
end-of-stream. Every later read or write by the server throws `IOException`, as after a drop.

| Connection type | What is sent and closed at N | Bytes the client keeps |
| --- | --- | --- |
| One TCP connection (HTTP/1.x, SMTP, IMAP, POP3, RTSP, WebSocket, Redis, MQTT ...) | The N-th byte is flushed, then the connection closes gracefully (FIN after a lingering close). | Exactly N. |
| FTP control + data | The count is the exchange's (control replies + file bytes). When it reaches N the write is cut at N, then **every joined connection** (the data connection being written, and the control connection) closes gracefully, in that order. No `426` or `451` is sent: a cut is a connection that ends, and `error` is the kind that answers. | Exactly N across both; a test sets N past the `150` to cut inside the file. |
| TLS (https, ftps, smtps, imaps ...) | N counts application bytes (the engine's wrapper sits above TLS). The graceful close sends **`close_notify`** (ADR-0122 decision 5: `SslStream.ShutdownAsync`), then FIN. A reset sends none, which is how a test tells the two apart. | Exactly N application bytes. |
| HTTP/2 | `after-bytes` is a connection count (framing included), as for every protocol: when it is reached the connection closes gracefully (`close_notify`, FIN) after the N-th byte. The engine sits below the frame writer, so no `GOAWAY` is invented; a stream-level early `END_STREAM` is not offered because it would make N a stream byte count, which `after-bytes` is not. The command-stage drop of ADR-0141 amendment 1 (a stream reset) is unchanged and takes no `close`. | Exactly N connection bytes. |
| HTTP/3 (QUIC) | The connection closes through the QUIC transport's graceful path (`CONNECTION_CLOSE` with no error, after the N-th byte is sent); `Abort` stays the error close. | Exactly N connection bytes. |
| Datagram flows (`SilencedAfterBytesDatagramFlow`: TFTP, DNS-style flows) | No FIN exists. `close=graceful` is accepted and behaves as `drop` does: the flow stops answering after N bytes. It is not refused, so one rule scoped by `scheme` or without scope works across a mixed set of listeners. | Exactly N. |

The cases the conformance task BL-1618 measures against pinned upstream curl 8.21.0, as `Integration` tests, recording
exit code and bytes kept: HTTP/1.1 with `Content-Length` (expected 18, to be confirmed), HTTP/1.1 chunked (no declared
size), FTP `RETR` (data and control closed gracefully; expected 18), the same over TLS, and the `close=reset` twin of each
(expected 56). **Fallback decided now so BL-1618 never blocks:** if curl exits 56 on the FTP `RETR` because the control
connection closes too, BL-1613 changes the FTP graceful cut to leave the control connection open and send
`426 Connection closed; transfer aborted.` after the data connection closes, and BL-1618 records that as an amendment to
this ADR.

### 3. Note and event

The same single `FaultAppliedEvent` as a drop (rule ID, `Drop`, stage `Accept`, exchange ID, listen URL), published once
per exchange. The exchange-log note differs, so a test can tell the two: the reset keeps
`Exchange <exchange id> dropped after <N> bytes sent by fault rule <rule id>.` and the graceful cut writes
`Exchange <exchange id> cut after <N> bytes sent by fault rule <rule id>, closing gracefully.`

### 4. The stand-in plan

- **No store is involved.** The cut changes only how the connection ends; a store that served the bytes is untouched.
- **Configured as data**, one spelling everywhere: `--fault` and the `-K` file (`drop;after-bytes=N;close=graceful`);
  `Surl.Api`: `SurlFault.Close` (`FaultClose`, default `Reset`) beside `AfterBytes`, written back by `ToString`;
  `Surl.Mcp`: an optional `close` argument of the `add_fault` tool; the loopback control endpoint's `/v1/faults`
  carries the same item text. A test resets by removing the rule, as for any fault.
- **Validation texts** (after `option <name>: `, in decision 4's style): `key close must be reset or graceful`,
  `key close does not apply to <kind>` (the existing text for a key on the wrong kind), and
  `key close needs key after-bytes`. `close=reset` with or without `after-bytes` is accepted (it is the default). The
  value is read in any case, like the kind and keys.
- **Help.** `--aihelp testing` (and the topics that carry the fault paragraph) and `--help testing` list the `close` key
  and its values in the key table and add an example, `drop;after-bytes=4000000;close=graceful` (a cut-off download); the
  `after-bytes` wording is unchanged.
- **Cross-protocol scenario it opens.** An FTP `RETR` is cut at N, then `curl -C -` resumes the file over FTP or HTTP
  from the same content store, which one test asserts end to end.

### 5. Implementation tasks

Each depends on BL-1608 and is sized to at most two projects:

| Task | Work | Projects |
| --- | --- | --- |
| BL-1611 | `FaultClose`, `FaultRule.Close` | `Surl.Protocol.Abstractions` |
| BL-1612 | `--fault` reader: `close`, its refusals, `ConfiguredFault` | `Surl.Cli` |
| BL-1613 | engine: graceful cut, note, every joined connection; `FaultPlan` carries `Close` | `Surl.Core` |
| BL-1614 | plan composition and item text | `Surl.Composition` |
| BL-1615 | `SurlFault.Close` | `Surl.Api` |
| BL-1616 | `add_fault`'s `close` | `Surl.Mcp.UnitLibrary` |
| BL-1617 | `--aihelp` and `--help testing` wording | `Surl.Cli` |
| BL-1618 | `Integration` conformance: curl 8.21.0's exit code and bytes kept | `Surl.Conformance` |

## Consequences

- A test gets both a vanished connection (`drop;after-bytes=N`, curl exit 56 expected) and a cut-off download
  (`close=graceful`, exit 18 expected for a declared size), from one rule shape.
- No existing configuration changes meaning; a build without the key behaves as before.
- Two exit codes in this ADR (18 and 56) are expectations from libcurl's documentation, not measurements, until BL-1618.
