# ADR-0134 — How the HTTP, RTSP and WebSocket servers answer a chosen error

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05, in BL-793.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decision 4 (the fault model, the
  `error` kind and its reply table), ADR-0032 (the HTTP authentication session), ADR-0045 (logins bound to the
  body), ADR-0071 (the WebSocket upgrade), ADR-0074 (RTSP), ADR-0133 (HTTP `PUT`).
- **Acted on by:** BL-793.

## Context

ADR-0124 decision 4 says each server family answers a matching `error` rule at the `Command` stage with the
rule's `reply` "in the protocol's own error shape", and that for HTTP, RTSP and WebSocket the command is the
method (the upgrade request for WebSocket) and the path the request path without its query. It does not say
where in each server the plan is asked, what fields the answer carries, whether the connection survives, or
what a WebSocket close code means for an upgrade. Those bytes were measured against pinned upstream curl 8.21.0
(win-x64, SHA-256 `0E773709…8778`) and its `libcurl-4.dll` with `Record-CurlExchange.ps1` before any test pinned
them; the recordings are the `fault-*` fixtures of the three test projects.

## Decision

1. **HTTP** (`HttpRequestAnswerer`, so HTTP/1.x, HTTP/2 and HTTP/3 alike). The plan is asked once per request
   after the authentication session let it in, before the upload limit and before the content store: for a
   login bound to the body (ADR-0045) only once the body check accepted it, so a fault never skips a login
   check and an account-scoped rule never fires for an unproven account. A chosen error is the empty answer a
   `404` is - `Date`, `Server: surl`, `Content-Length: 0`, any challenges - with the reply's status and reason
   phrase, and it keeps the connection; when the request announced a body that was not read it is a refusal
   with `Connection: close`, as a `401` is (ADR-0032 section 4). A `PUT`'s upload is left uncommitted.
   Measured: `GET /a` answered `HTTP/1.1 503 Service Unavailable` this way exits 0, and curl sends `GET /b`
   on the same connection; with `--fail` it exits 22, `The requested URL returned error: 503`.
2. **RTSP** (`RtspRequestResponder`). The plan is asked after the login verdict lets the request in (ADR-0074
   decision 2, check 4), its body already read, before the method's own checks, the content store and the
   session. A chosen error is answered as every refusal that keeps the connection: status line, `CSeq`,
   `Date`, `Server: surl`, the challenges and `Session` when the request named the live one, no body. An
   `ANNOUNCE`'s upload is left uncommitted. The path is the Request-URI's path for an `rtsp://` URI and none
   for `*`. Measured: libcurl's `DESCRIBE` answered `RTSP/1.0 503 Service Unavailable` ends `CURLcode 0`,
   status 503; the `curl` tool's `OPTIONS` with `-f` exits 22.
3. **WebSocket** (`WebSocketUpgradeResponder`). A `ws`/`wss` reply is a close code, so the upgrade is not
   refused with an HTTP status: the plan is asked after the login and ADR-0071 decision 1's field checks pass
   (only a valid upgrade can be accepted), before the path is looked up; a chosen error is the `101` the
   upgrade would get, then a `CLOSE` carrying the rule's code and reason as the server's first and only
   frame, then the half-close and the lingering close. Measured: `\x88\x0B\x03\xF5try later` after the `101`
   exits 0, and curl writes the close payload to standard output.
4. **What fits.** HTTP and RTSP read a status 400 to 599 and, after one space, an optional reason phrase of
   tabs, spaces and visible ASCII characters; without a phrase the server sends RFC 9110's (HTTP) or RFC 2326
   section 7.1.1's (RTSP) phrase for the code, or an empty one for a code neither names, which curl accepts
   (`HTTP/1.1 599 ` and `RTSP/1.0 599 ` both measured, exit 0). WebSocket reads a code RFC 6455 section 7.4
   allows on the wire - 1000 to 1014 less 1004 to 1006, and 3000 to 4999 - and an optional non-empty reason
   of at most 123 UTF-8 bytes; the codes 1004 and 1016 to 2999 that ADR-0124's reply table lets through are
   reserved and are not sent. A reply that does not fit, which a `Surl.Api` hook could hand over unchecked, is
   noted in the exchange log and not applied: the request is answered as without the rule and no event is
   published.
5. **Only the `error` kind.** These servers apply `Error` at the `Command` stage. A `delay` or `drop` rule
   given `stage=command` that matches is noted as not applied here and the request goes on; applying those at
   the command stage needs the engine's idle clock paused (delay) and a way to abort from an HTTP answer
   (drop), filed as follow-up work.
6. **Events and the point.** Each application publishes one `FaultAppliedEvent` (stage `Command`, the
   exchange's ID, the path as its target, or the method when there is no path) and notes the answer in the
   exchange log. The `FaultPoint` carries the exchange ID, the listen URL formatted as the serving engine
   formats it (bound port included), the scheme, the account (none when anonymous), the path and the method.
   `Surl.HttpMessage`'s `HttpCommandFaults` builds the point, reads the reply and publishes, for all three.

## Consequences

A test can make one HTTP, RTSP or WebSocket request fail on purpose and see it on the event stream, with the
connection behaving as curl expects after it. No store changes. The command-stage delay and drop, and a
reply check in `Surl.Cli` that refuses the reserved WebSocket close codes, are follow-up tasks.
