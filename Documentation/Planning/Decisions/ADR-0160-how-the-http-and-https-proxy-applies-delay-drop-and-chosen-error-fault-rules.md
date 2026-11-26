# ADR-0160 — How the HTTP and HTTPS proxy applies delay, drop and chosen-error fault rules

- **Status:** Accepted
- **Date:** 2026-11-14
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-14, in BL-1153.
- **Builds on:** [ADR-0124](ADR-0124-surl-is-a-stand-in-for-tests.md) decision 4 (the fault model),
  [ADR-0088](ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md) (the proxy),
  [ADR-0134](ADR-0134-how-the-http-rtsp-and-websocket-servers-answer-a-chosen-error.md) (HTTP chosen errors),
  [ADR-0141](ADR-0141-how-a-server-applies-a-command-stage-delay-or-drop.md) (command-stage delay and drop, and its
  amendment 1 for HTTP/2 stream resets).
- **Amends:** ADR-0124 decision 4 (two scheme names, one `path` row, two reply rows).
- **Acted on by:** BL-1173 (the wiring) and BL-1174 (measuring pinned upstream curl 8.21.0 against it).

## Context

`Surl.Protocol.HttpProxy.UnitLibrary` never asks `ExchangeContext.Faults`. The engine's accept-stage delay and drop
and the shared refuse-login policy reach it, but a test cannot make the proxy answer a `CONNECT` with a 5xx, hold a
forwarded request or cut a tunnel. A proxy listener's listen URL has scheme `http` or `https`, the same as the HTTP
server's, so a rule `error;scheme=http;reply=503` written for the HTTP server would, if the proxy asked with the
listen URL's scheme, silently change every proxy too. The point's `Scheme`, `Command` and `Path` need deciding.

## Decision

1. **Where the proxy asks, and the point.** One `FaultPoint`, stage `Command`, per `CONNECT` request, per forwarded
   request (absolute-form `http://` or `ftp://`, HTTP/1.x) and per HTTP/2 proxy stream, asked through the same shared
   helper the HTTP server uses (`HttpCommandFaults`, ADR-0134 decision 6) so the exchange ID, the listen URL
   (`ReportedListenUrl`) and the event are built the same way. It is asked:
   - after the proxy login verdict lets the request in (ADR-0088 amendment 3), so a rule never skips a login and an
     `account` rule never fires for an unproven account;
   - after the request target was read and found well formed (a malformed target is the proxy's own `400` and is not
     fault-eligible);
   - before the origin connection is opened, before any request body or `Expect: 100-continue` is read, and before the
     previous origin link is dropped, so a drop leaves no origin connection and a delay holds nothing open.

   The point carries: **scheme** `http-proxy` for an `http` listener and `https-proxy` for an `https` listener
   (decision 2); **command** the method as sent, uppercase, `CONNECT` included; **path** the request target without
   query or fragment - `host:port` for `CONNECT` (IPv6 literals in brackets), and for a forwarded request the whole
   absolute-form target, scheme and authority kept (`http://example.test/a`, `ftp://example.test/pub/f`) so a test can
   pick an origin; the segment-prefix rule of ADR-0124 then makes `path=http://example.test` match everything on that
   origin; **account** the proxy account the login judged, none when anonymous; **exchange ID** and **listener** as for
   every server.
2. **How `scheme` and `listener` rules reach the proxy.**
   - `http-proxy` and `https-proxy` are two new scheme names ADR-0124's `scheme` key accepts, the proxy's own, not URL
     schemes a client connects with (a proxy is chosen by `--proxy`, not by scheme, ADR-0088 decision 7). They are the
     only schemes the proxy's command-stage point carries.
   - So `error;scheme=http` and `error;scheme=https` **never** reach a proxy: the HTTP server's rules stay the HTTP
     server's, and nothing changes for an existing configuration. A rule for a proxy says `scheme=http-proxy`
     (or `https-proxy`), or names the proxy's `listener=`, which matches whichever scheme the point carries, as for any
     server. `error` still needs `scheme` or `listener` (ADR-0124); `listener=http://127.0.0.1:8888` on an HTTP
     proxy listener is accepted and matches only that listener's proxy exchanges.
   - **Accept stage is unchanged.** The engine's accept-stage point keeps the listen URL's scheme (`http` or `https`),
     as it does today: a rule has no command, path or account there, a test scopes an accept-stage fault to one
     proxy with `listener`, and changing what `scheme=http` matches at accept would change existing behaviour.
   - The tables' "unknown scheme" check grows by the two names; `--aihelp`'s `testing` and proxy topics list them.
3. **The chosen error.** `reply` is a status 400 to 599 and an optional reason phrase, exactly the `http` shape
   (ADR-0134 decision 4), so `FaultReplyShapes` gains two rows, `http-proxy` and `https-proxy`, that read as `http`
   does; no new parser. The answer is the proxy's own answer (ADR-0088: `Date`, `Server: surl`, a fixed-text body naming
   the status per ADR-0006 section 3, `Content-Length`), with the rule's status and reason phrase, written by the
   same `AnswerAsync` path every proxy-made answer uses:
   - to `CONNECT`: sent in place of `HTTP/1.1 200 Connection established`; no origin connection is made, no tunnel
     opens; the connection closes as the proxy's `502` for an unreachable origin does (`RequestEndsConnection`).
     A chosen `407` carries no `Proxy-Authenticate` challenge (a login challenge is the policy's, not a fault's).
   - to a forwarded request: the same answer, keeping the connection when the request announced no unread body and
     closing it with `Connection: close` when it did, as ADR-0134 decision 1 does; the origin is never contacted.
   - on an HTTP/2 proxy stream: the same status as `:status` with the fixed body, the stream closed, the connection
     kept.
   Free-form `reply` text that does not fit is not applied and noted, as ADR-0134 decision 4. `FaultAppliedEvent`
   target is the point's path.
4. **Delay and drop.** Both apply at the command stage through `CommandStageFaults.FindAndApplyAsync` (ADR-0141):
   - **Before a tunnel exists** (the point of decision 1): `delay` holds the request on `TimeProvider` with the idle
     clock paused, then the proxy goes on as without the rule - for `CONNECT` the `200` is sent later; `drop` aborts
     the client connection with no byte sent and no origin connection opened (HTTP/2: reset the stream with
     `INTERNAL_ERROR`, ADR-0141 amendment 1, the connection and its other streams carry on).
   - **After a tunnel is established** the proxy asks no further command-stage point: the bytes inside a tunnel are
     the client's own protocol, not proxy requests, and a command-stage `delay` inside it would be a protocol
     alteration. A test cuts an open tunnel with `drop;after-bytes=<n>` (the engine's bytes-sent drop, which counts the
     `200` reply's 39 bytes), and BL-1173 makes sure the tunnel's relay then ends by the same path as any failed
     tunnel, aborting the origin link too (`The tunnel ... failed; both connections were aborted.`). It cannot slow a
     tunnel; a test that wants a slow origin puts a delay on the origin's own listener.
5. **Expected upstream curl 8.21.0 results**, read from upstream's documentation of its error codes
   (<https://curl.se/libcurl/c/libcurl-errors.html>, the 8.21.0 page - not measured yet; BL-1174 measures each with
   `Record-CurlExchange.ps1` and the pinned build, and a measured result replaces the expectation here by amendment):

   | Case | Expected exit |
   | --- | --- |
   | `CONNECT` answered with a chosen 4xx or 5xx (including 407) | 56, `CURLE_RECV_ERROR` ("CONNECT tunnel failed, response N"); `--fail` does not change it |
   | Forwarded request answered with a chosen 4xx or 5xx | 0 with the body; 22, `CURLE_HTTP_RETURNED_ERROR`, with `--fail` |
   | `CONNECT` dropped (reset, no bytes) | 56, `CURLE_RECV_ERROR` (52, `CURLE_GOT_NOTHING`, if the measurement shows a clean close) |
   | Forwarded request dropped | 56 (52 if the close is clean) |
   | Tunnel cut by `after-bytes` during the inner TLS handshake | 35, `CURLE_SSL_CONNECT_ERROR`; after it, 56 |
   | `delay` longer than `-m`/`--max-time` | 28, `CURLE_OPERATION_TIMEDOUT`; shorter, 0 |

   `CURLE_PROXY` (97) is not expected: upstream reserves it for the SOCKS handshake (BL-1061), not HTTP proxies.

## Consequences

- A test can write `error;scheme=http-proxy;command=CONNECT;path=example.test:443;reply=503`, or
  `delay;stage=command;scheme=https-proxy;ms=500`, and get a refused, slow or cut-off proxy exchange, one
  `FaultAppliedEvent` each, while every existing `scheme=http` rule keeps meaning the HTTP server only.
- `Surl.Cli`, `Surl.Api`'s `SurlFault` and the control endpoint accept the two scheme names through the same
  scheme list; BL-1173 adds them there and in `--aihelp` with the proxy library's wiring.
- No byte Surl sends outside a fault changes. A chosen error uses the answer shape already measured for the
  proxy's own `502`; BL-1174 measures the exit codes above.

## Alternatives considered

- **Carry `http`/`https` as the point's scheme and add a proxy flag to `FaultPoint`.** Rejected: it changes a contract
  every server shares for one family, and `scheme=http` would still reach proxies unless every matcher learns the flag.
- **Let `scheme=http` match both.** Rejected: an HTTP-server rule silently altering a proxy listener in the same
  instance is the failure the task exists to prevent.
- **A command-stage point per tunnel chunk.** Rejected: it would treat the client's inner protocol as proxy commands;
  `after-bytes` already cuts a tunnel.

## Amendment 1 — measured with pinned upstream curl 8.21.0 (BL-1174, 2026-11-15)

`StandInProxyFaultRecipeTests` measured the table of decision 5 on Windows:

- `CONNECT` answered with a chosen 503: **exit 7** (`CURLE_COULDNT_CONNECT`), stderr `curl: (7) CONNECT tunnel failed, response 503`, not 56. The expectation is replaced.
- Forwarded request answered 503 with `--fail`: exit 22, `curl: (22) The requested URL returned error: 503`, as expected; a `times=1` rule fails only the first run and the retry gets the file.
- Forwarded request dropped at the command stage: exit 56, `Recv failure: Connection was reset`, as expected (the test also accepts 52 and 55 for Linux and macOS, as `DropConnection` does).
- `delay` of 5 s with `--max-time 1`: exit 28, `Operation timed out`, as expected.

## Amendment 2 — a chosen `407` carries the proxy's challenges (BL-1725)

Decided by Claude under Stewart's delegation, 2026-11-25. Found during BL-1700.
[RFC 9110 section 15.5.8](https://www.rfc-editor.org/rfc/rfc9110#section-15.5.8) says a proxy sending `407` MUST
send `Proxy-Authenticate`, and [section 11.7.1](https://www.rfc-editor.org/rfc/rfc9110#section-11.7.1) that it
sends at least one in each `407` it generates. This **replaces** decision 3's sentence "A chosen `407` carries no
`Proxy-Authenticate` challenge (a login challenge is the policy's, not a fault's)." The challenges are still the
policy's: the fault only asks for them.

- **D1.** A chosen status `407`, whatever its reason phrase, carries the values of the connection's proxy login
  session's `IHttpAuthenticationSession.CreateChallenges()`, one `Proxy-Authenticate` field per value, in order:
  to `CONNECT`, to a forwarded request and on an HTTP/2 stream. On HTTP/2, whose streams share the session, it is
  asked under the same gate the login is judged under.
- **D2.** When the session offers none (the contract's default, as `AnonymousAuthenticationPolicy`'s session), the
  `407` goes without the field and the exchange log notes
  `fault rule <id> chose 407, and the proxy offers no challenge on this connection`.
- **D3.** Over HTTP/2, NTLM and Negotiate challenges, even when they are the only ones offered, are sent like any
  other: no stream is filtered or reset. The proxy's own login `407` over HTTP/2 sends every value of its verdict,
  so a chosen `407` matches it. This differs from the HTTP server, which resets such a `401` with
  `HTTP_1_1_REQUIRED` ([ADR-0081](ADR-0081-what-upstream-curl-expects-for-ntlm-and-negotiate-over-http-2-and-http-3.md)
  decision 7, [ADR-0134](ADR-0134-how-the-http-rtsp-and-websocket-servers-answer-a-chosen-error.md) Amendment 1).
  The proxy's login answers are not changed. What upstream curl does with them over HTTP/2 to a proxy has not been
  measured, and nothing is claimed about it.
- **D4.** Every other chosen status is unchanged, and a chosen `401` is not given `WWW-Authenticate`. Whether the
  connection closes after the answer is unchanged.

No new recording from pinned upstream curl 8.21.0 was made: the field has the shape of the proxy's login `407`,
already pinned by the fixtures [ADR-0088](ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md)
Amendment 3 describes.

### Consequences

A test seeds a chosen `407` with `error;scheme=http-proxy;reply=407` (or `https-proxy`), or its `Surl.Api`
equivalent, and sees in the response the challenges the proxy's own `407` would offer. The rule publishes the same
one `FaultAppliedEvent`; no store changes.
