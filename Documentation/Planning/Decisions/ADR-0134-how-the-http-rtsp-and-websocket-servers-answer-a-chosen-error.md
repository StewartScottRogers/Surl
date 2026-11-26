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
   of at most 123 UTF-8 bytes; the codes 1004 and 1016 to 2999 that ADR-0124's reply table let through until BL-903 are
   reserved and are not sent. A reply that does not fit, which a `Surl.Api` hook could hand over unchecked, is
   noted in the exchange log and not applied: the request is answered as without the rule and no event is
   published.
5. **`error`, `delay` and `drop`.** These servers apply `Error` at the `Command` stage, and, since BL-902,
   `Delay` and `Drop` as [ADR-0141](ADR-0141-how-a-server-applies-a-command-stage-delay-or-drop.md) amendment 1
   says. Any other kind that matches (`RefuseLogin`, `OutOfSpace`) is noted as not applied here and the request
   goes on.
6. **Events and the point.** Each application publishes one `FaultAppliedEvent` (stage `Command`, the
   exchange's ID, the path as its target, or the method when there is no path) and notes the answer in the
   exchange log. The `FaultPoint` carries the exchange ID, the listen URL formatted as the serving engine
   formats it (bound port included), the scheme, the account (none when anonymous), the path and the method.
   `Surl.HttpMessage`'s `HttpCommandFaults` builds the point, reads the reply and publishes, for all three.

## Consequences

A test can make one HTTP, RTSP or WebSocket request fail on purpose and see it on the event stream, with the
connection behaving as curl expects after it. No store changes. The command-stage delay and drop, and a
reply check in `Surl.Cli` that refuses the reserved WebSocket close codes, are follow-up tasks.

## Amendment 1 (BL-1700)

Decided by Claude under Stewart's delegation, 2026-11-25. From item 3 of the Surly bug report
`2026-11-25T1430-Surl-bug-report.md`: a chosen `401` on a request let in carried no `WWW-Authenticate`, though
[RFC 9110 section 11.6.1](https://www.rfc-editor.org/rfc/rfc9110#section-11.6.1) says a server generating a
`401` must send at least one challenge. This amends the HTTP answer of decision 1 only. Decision 1's
`Content-Length: 0` no longer describes that answer's body: see
[ADR-0186](ADR-0186-the-http-servers-own-4xx-and-5xx-refusals-carry-a-short-fixed-text-body.md).

- **D1.** A chosen status `401`, whatever its reason phrase, over HTTP/1.x, HTTP/2 and HTTP/3 alike, carries the
  values of the connection's `IHttpAuthenticationSession.CreateChallenges()`, one `WWW-Authenticate` field per
  value, in order. They replace the let-in verdict's values: those are only login success tokens (Negotiate's
  final token), not challenges, so they are not sent on a chosen `401`.
- **D2.** When the session offers none on this connection, the `401` goes without one, knowingly, as the server has
  no challenge a client could answer, and the exchange log notes
  `fault rule <id> chose 401, and the server offers no challenge on this connection`. `Surl.Authentication`'s
  session offers the challenges of the HTTP methods `--auth` accepts, less `basic` and `bearer` on a connection
  without TLS and without `--allow-plaintext-auth`; `aws-sigv4` has no challenge. So it offers none only when no
  accepted method is left with a challenge. The accounts and `--allow-anonymous` do not change the list: with no
  account and the default `--auth`, a chosen `401` still carries the Digest challenges. A session that keeps the
  contract's default `CreateChallenges`, as `AnonymousAuthenticationPolicy`'s does, offers none too. Where
  `Surl.Authentication`'s session has to refuse and has no challenge to offer, it answers `403`, so with that
  session only a fault rule produces a `401` without a challenge.
- **D3.** No other chosen status gains a challenge: each sends the verdict's values as before. A chosen `407` is
  not given `Proxy-Authenticate`, since the `http` server is not a proxy.

Measured by the conformance audit with pinned upstream [curl](https://curl.se/docs/manpage.html) 8.21.0 (win-x64,
SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`) against canned replies modelled on
this answer (a `401` with Bearer and Basic challenges; no fixtures added): `--oauth2-bearer tok` makes one request
and exits 0; with `--fail` it exits 22; `-u u:p --anyauth`, given the same reply to every request, makes two
requests on one connection, then exits 0 with no third.

### Consequences

`ConnectionBoundLogins` now compares the status code, so a `401` with a reason phrase of its own counts as a
`401`: a chosen `401` whose every challenge is NTLM or Negotiate is reset over HTTP/2 (`HTTP_1_1_REQUIRED`) or
HTTP/3 (`H3_VERSION_FALLBACK`) like the server's own `401`. The client then never receives the chosen `401` or a
`body=` text, though the `FaultAppliedEvent` is published and one of the rule's `times` is spent.

## Amendment 2 (BL-1724)

Decided by Claude under Stewart's delegation, 2026-11-25. Found during BL-1700: a chosen `401` on an RTSP request
let in carried only the let-in verdict's values, so no challenge, though
[RFC 9110 section 11.6.1](https://www.rfc-editor.org/rfc/rfc9110#section-11.6.1) says a server generating a `401`
must send at least one, and [RFC 2326](https://www.rfc-editor.org/rfc/rfc2326) section 1.4 applies HTTP
authentication to RTSP. This amends the RTSP answer of decision 2 only, mirroring Amendment 1.

- **D1.** A chosen status `401`, whatever its reason phrase, carries the values of the connection's
  `IHttpAuthenticationSession.CreateChallenges()`, one `WWW-Authenticate` field per value, in order. They replace
  the let-in verdict's values: those are only login success tokens, not challenges.
- **D2.** When the session offers none on this connection, the `401` goes without the field and the exchange log
  notes `fault rule <id> chose 401, and the server offers no challenge on this connection`. When
  `Surl.Authentication`'s session offers none is as Amendment 1 D2 says.
- **D3.** Every other chosen status is unchanged: it sends the verdict's values as before. A chosen `407` is not
  given `Proxy-Authenticate`, since the `rtsp` server is not a proxy.

The rest of the answer (`CSeq`, `Date`, `Server`, `Session`, the connection kept, no body) is unchanged. No new
recording from pinned upstream curl was made: the field has the shape of the login `401` that the RTSP fixture
`no-login-401` already pins.

### Consequences

A test seeds a chosen `401` with a `--fault` rule, or its `Surl.Api` equivalent, and sees in the response the
challenges the server's own `401` would offer. The rule publishes the same one `FaultAppliedEvent`; no store
changes.
