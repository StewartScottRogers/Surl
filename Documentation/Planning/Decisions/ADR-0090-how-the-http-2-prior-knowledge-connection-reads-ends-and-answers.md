# ADR-0090 — How the HTTP/2 prior-knowledge connection reads, ends and answers

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-368, which builds it.
- **Refines:** [ADR-0081](ADR-0081-how-surl-serves-http-2.md) decisions 3, 5 and 7 where they
  leave a choice open, and applies
  [ADR-0059](ADR-0059-how-a-protocol-server-tells-a-limit-from-shutdown.md) to HTTP/2 (decision 4).
  Every byte ADR-0081 pins is unchanged.

## Context

BL-368 serves HTTP/2 started with prior knowledge on `http://`. ADR-0081 decides the settings,
the limits and their error codes, and what carries over from HTTP/1.1; building it met a few
questions that ADR does not answer. Upstream curl's side was measured for ADR-0081, and once more
for this task: `Fixtures/h2-prior-knowledge-get-file` in `Surl.Protocol.Http.UnitTests` is the
static 8.21.0 build's `curl -sS -i --http2-prior-knowledge http://127.0.0.1:18050/file.txt`, 173
bytes, the same frames as ADR-0081's "Measured" table with `:path /file.txt`.

## Decision

1. **The preface is read only on cleartext connections, within the head timeout.** A connection
   secured with TLS goes straight to HTTP/1.x until BL-370 offers `h2` by ALPN. On a cleartext
   one, reads of up to 4096 bytes - the HTTP/1.x head reader's first buffer, so a request that
   arrives at once is still read in one read and the exchange log is unchanged - go on while the
   bytes are a prefix of `PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n`. The whole preface starts HTTP/2, and
   any bytes read past it are the first frames; anything else is handed to the HTTP/1.x reader
   with the bytes read first. The head timeout times this read: with no byte read it closes with
   the HTTP/1.x note `No request head was read: HeadTimedOutBeforeAnyByte; closed with no bytes.`,
   and with part of the preface read it closes with no bytes too (ADR-0081, decision 5). The
   HTTP/1.x reader then starts its own head timeout, so an HTTP/1.x request whose first bytes
   dribble in matching the preface may take up to two head timeouts; only `P`, `PR`, ... prefixes
   arriving in separate reads do that.
2. **One reader, one task per stream, one writer.** The connection reads frames in order on one
   loop. Each request stream is answered by `HttpRequestAnswerer` on a task of its own, so a
   stream waiting for its body or for window never holds up another. Every frame goes through
   one writer that holds the connection for a whole frame, and for a whole header block with its
   `CONTINUATION`s; the HPACK encoder encodes inside that hold, so blocks reach the client in the
   order its decoder expects. Writes are cut off only by the exchange's own cancellation, never a
   stream's, so a reset stream never leaves half a frame on the wire.
3. **Flow control.** The server never sends a frame longer than 16384 bytes, the least
   `SETTINGS_MAX_FRAME_SIZE` a client may advertise, so it ignores the client's larger value.
   The connection's receive window is replenished as each `DATA` frame arrives, the padding of a
   stream's frame at once, and the content as the handler reads it, so a handler that does not
   read holds the client back to its 65535-byte stream window and no more. When the client
   closes, open streams send what the windows they already have allow; a stream that needs more
   ends with the log note `Stream <n> ended before its answer was sent: ...` and no further frame.
4. **The end of the exchange.** On a limit's cancellation (`--idle-timeout`, `--max-time`) and at
   shutdown alike the open streams are cut off and `GOAWAY` naming the highest stream received,
   `NO_ERROR`, is written within a one-second deadline of the server's own, as ADR-0081's
   shutdown row says. ADR-0059 decision 4 has shutdown end an exchange with no farewell because
   its write window is linked to the shutdown token; for HTTP/2 the deadline is not linked to it,
   so the `GOAWAY` gets its second even at shutdown, after which the cancellation escapes as
   ADR-0059 requires. A limit's cancellation does not escape. There is no grace for open streams
   to finish: the engine gives no signal before it cancels the exchange.
5. **Errors ADR-0081 does not name.** A decoded header list past `--max-message`
   (`HpackDecodingError.HeaderListTooLarge`) is the same limit as a block past it, `GOAWAY`
   `ENHANCE_YOUR_CALM`; every other HPACK failure is `COMPRESSION_ERROR`. A header block on a
   stream number already used is trailers: on an open stream whose body is still arriving and
   with `END_STREAM` it ends the body, otherwise it resets the stream `PROTOCOL_ERROR`; on a
   closed stream it is decoded, to keep the table in step, and ignored, which also covers frames
   the client sent before it saw the server's `RST_STREAM` (RFC 9113 section 5.4.2). `DATA` on a
   closed stream is ignored but still counted and replenished on the connection's window
   (RFC 9113 section 6.9). `DATA` after the client's `END_STREAM` on a stream still being answered
   is `RST_STREAM` `STREAM_CLOSED`. A frame on a stream never opened (`DATA`, `WINDOW_UPDATE`,
   `RST_STREAM`), a `HEADERS` on an even stream, a client's `PUSH_PROMISE`, a `CONTINUATION` with
   no block or of another stream, and a first frame that is not `SETTINGS` are `GOAWAY`
   `PROTOCOL_ERROR`. A stream `WINDOW_UPDATE` past 2^31-1 is `RST_STREAM` `FLOW_CONTROL_ERROR` and
   a connection one, or a `SETTINGS_INITIAL_WINDOW_SIZE` that would push an open stream's window
   past it, `GOAWAY` `FLOW_CONTROL_ERROR`.
6. **What waits for later tasks.** The `503` refusal of a connection past a connection limit is
   still written as HTTP/1.1 bytes (ADR-0081's "Too many connections" row; BL-601), and NTLM and
   Negotiate over HTTP/2 are not yet answered `HTTP_1_1_REQUIRED` (decision 7; BL-602). Both are
   filed as their own tasks, and `surl --aihelp http` says what is answered today. Both are now
   built: the `503` by BL-601 (Amendment 1) and the `HTTP_1_1_REQUIRED` reset by BL-602
   (Amendment 2).

## Alternatives considered

- **Sniffing the preface inside the HTTP/1.x reader** (treat `PRI * HTTP/2.0` as a request line
  and switch on it): rejected, it needs `Surl.HttpServing` and `Surl.HttpMessage` to know about
  HTTP/2, which ADR-0084 keeps out of them.
- **Answering each stream on the reader loop**: rejected, a `GET` whose body is still arriving
  would stop every other stream.
- **Replenishing the connection window only as handlers read**: rejected, a stream whose handler
  does not read would stall every other stream once 65535 bytes were in flight.

## Consequences

- `HttpProtocolServer` serves HTTP/2 with prior knowledge on `http://`; every existing HTTP/1.x
  expected byte is unchanged.
- BL-369 (`Upgrade: h2c`) and BL-370 (ALPN `h2`) hand a connection to the same
  `Http2ConnectionServer`; BL-409 moves it to `Surl.Http2` (ADR-0086).
- BL-371 measures curl's exit code against a live surl for decisions 4 and 5.

## Amendment 1 - the HTTP/2 `503` refusal is answered (BL-601, 2026-11-11)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-11.

The refusal half of decision 6 is done. BL-601 made `HttpProtocolServer.WriteRefusalAsync` read
the first bytes with `Http2ConnectionPreface`; when the whole preface arrives it refuses through
`Surl.Http2`'s `Http2ConnectionRefusal`, as ADR-0081 decision 5's "Too many connections" row says.
What is sent, in order: an empty `SETTINGS`; `HEADERS` with `:status 503`, `server: surl` and
`content-length: 0`, carrying `END_STREAM`, on the client's first stream; then `GOAWAY` naming that
stream with `NO_ERROR`. A client that sends no `HEADERS` before the engine's refusal deadline, or
no bytes at all, is closed with no bytes. A connection that is not HTTP/2 is still refused with
HTTP/1.1 bytes, unchanged.

Decision 6 therefore no longer says the `503` is "still written as HTTP/1.1 bytes" for an HTTP/2
client. What still waits is `HTTP_1_1_REQUIRED` for NTLM and Negotiate over HTTP/2 (decision 7;
BL-602). BL-859 files this amendment because BL-574 held this folder while BL-601 ran.

## Amendment 2 - NTLM and Negotiate over HTTP/2 are reset `HTTP_1_1_REQUIRED` (BL-602, 2026-11-05)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-11.

The last half of decision 6 is done. BL-602 (2026-11-05) made `Http2StreamAnswerWriter` in
`Surl.Protocol.Http` reset a stream `RST_STREAM` `HTTP_1_1_REQUIRED`, as ADR-0081 decision 7 says,
in two cases: its `authorization` field names NTLM or Negotiate (`ConnectionBoundLogins.AreCarriedBy`),
checked before the answerer sees the request, so no handshake starts; or its answer is a `401`
whose challenges offer only NTLM or Negotiate (`ConnectionBoundLogins.AreTheOnlyLoginsOfferedBy`).
Curl then retries the request over HTTP/1.1, where the login goes ahead; the HTTP/2 connection
carries on. ADR-0081 decision 7 is unchanged, and nothing in decision 6 still waits.
`surl --aihelp http` says so (BL-839, which files this amendment because
`Documentation/Planning/Decisions` was held by BL-569 while BL-602 ran).
