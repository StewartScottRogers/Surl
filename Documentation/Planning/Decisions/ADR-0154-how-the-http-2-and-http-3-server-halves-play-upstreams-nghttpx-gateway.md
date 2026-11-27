# ADR-0154 — How the `http/2` and `http/3` server halves play upstream's nghttpx gateway

- **Status:** Accepted
- **Date:** 2026-11-14
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-14,
  in BL-1113 (FR-053). Built by BL-1114 to BL-1119.
- **Amends:** [ADR-0080](ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md)
  decision 2: the `http/2` and `http/3` row of its table ("Phase 6's HTTP/2 and HTTP/3 servers,
  once built | verbatim reply") becomes decisions 1 to 7 below. Its `http/2` and `http/3` rows in
  `UpstreamTestCaseServers.All` say built once BL-1118 and BL-1119 land.
- **Amended:** 2026-11-27 in BL-1856, by
  [ADR-0080](ADR-0080-how-surl-plays-the-server-half-of-upstream-curls-test-cases.md)'s amendment of
  that date, decision 4's `%HTTP2TLSPORT` row: it serves the certificate a case's `https`
  `<server>` line names, and `test-localhost` when the case names none.

## Context

Two server names in upstream's corpus are not a protocol server answering from a case's `<reply>`.
At tag `curl-8_21_0`, `tests/http2-server.pl` starts **nghttpx as a reverse proxy in front of the
case's `http` server** (`sws`): `--backend=127.0.0.1,<HTTPPORT>`, a plaintext frontend on
`<HTTP2PORT>` (`no-tls`), a TLS frontend on `<HTTP2TLSPORT>` with `certs/test-localhost.key` and
`.pem`, `--conf=tests/nghttpx.conf` and `--backend-keep-alive-timeout=500ms`.
`tests/http3-server.pl` does the same for `<HTTP3PORT>` over QUIC. `tests/servers.pm` starts the
`http` server first whenever a case names `http/2` or `http/3`. So what a case verifies is nghttpx's
behaviour as well as curl's:

- `<protocol>` is the HTTP/1.1 request **as nghttpx forwarded it** to `sws` (test 1700 strips
  `^X-Forwarded-Proto:.*` and `^Via:.*`; test 2500 shows `GET https://localhost:%HTTP3PORT/2500
  HTTP/1.1`);
- `<stdout>` is the HTTP/2 or HTTP/3 response nghttpx built from the reply: lower-cased fields,
  `Connection:` gone, `via: 1.1 nghttpx`, and in test 2403 `server: nghttpx`.

nghttpx is not installed on any reference machine and downloading it is not ours to do
(CLAUDE.md, "Decisions"), so this ADR's rules come from what the six runnable cases' own `<protocol>`
and `<stdout>` show (read from `Conformance/Surl.Conformance.UnitTests/UpstreamTestCases/data`, the
import of tag `curl-8_21_0`) and from nghttpx's manual. A rule no case pins is marked
**unpinned**: it is Claude's choice, written down so a later case that pins it can change it.

**The 16 cases at the tag that name `http/2` or `http/3`**, checked 2026-11-14:

- Can run on a pin with the HTTP2 or HTTP3 feature: 1700, 1701, 1702 (`h2c`, the `Upgrade: h2c`
  path), 2403 (`http/2 SSL headers-api`), 2500 and 2503 (`http/3 nghttpx-h3`).
- Never run: 2400, 2401, 2406, 2501, 358 and 359 need `Debug`; 2402, 2404, 2407 and 2502 are libtests.

**The pins.** The Windows reference build (curl 8.21.0, Schannel) has no HTTP2 or HTTP3 feature, so
the six cases skip there with `feature-missing`; they run on the `linux-x64` and `osx-arm64`
reference pins (both list `HTTP2 HTTP3`, OpenSSL 4.0.1, nghttp2 1.69.0, ngtcp2 1.23.0) in CI's
`Upstream test cases` job.

**Correction to BL-1116's text.** Test 1700 is *not* "a plain request, then an upgrade": `--http2`
applies to both URLs, so the first request carries `Upgrade: h2c`, is answered `101` and becomes
stream 1 (`<data>`, "-foo-"), and the second URL is stream 3 of the same connection (`<data1>`,
"-maa-"). Both reach the backend as `GET` requests with no `Upgrade`. BL-1116's Context and
criteria are updated in the same commit.

## Decision

**1. Forwarding goes over a loopback socket to the case's own `http` half.** Each stream's request
is written to a fresh connection to `127.0.0.1:%HTTPPORT`, opened through the injected
`IOriginConnector` (`SocketOriginConnector(AllowedOriginRule.LoopbackOnly)`, BL-556), the way the
`http-proxy` half reaches its origin. The gateway never constructs a `Socket` (root `CLAUDE.md`). It
is not in process, because the `http` half is where the case's reply selection (`<dataNUM>`,
`<servercmd>`), the verbatim reply bytes and the recording of the request for `<protocol>` live
(ADR-0080 decision 2); going round it would re-implement them and record nothing. One backend
connection per stream, closed after the reply: **unpinned** (nghttpx reuses a connection for 500 ms,
but `sws` answers `Connection: close` in every case, so reuse never happens in the six cases).
Whenever a case names `http/2` or `http/3`, the run composes the `http` half too, as `servers.pm`
starts it.

**2. The HTTP/1.1 request the gateway writes.**

| Part | Rule | Pinned by |
| --- | --- | --- |
| Request line, HTTP/2 | `<:method> <:path> HTTP/1.1`, origin form | 1700, 1701, 1702 (`GET /1700`) |
| Request line, HTTP/3 | `<:method> <:scheme>://<:authority><:path> HTTP/1.1`, absolute form | 2500 (`GET https://localhost:%HTTP3PORT/2500 HTTP/1.1`) |
| `Host` | first field, `<:authority>` (the `Host` field if there is no `:authority`) | 1700, 2500 |
| Client fields | in arrival order after `Host`, names as the client sent them lower-cased by HTTP/2 and HTTP/3, written with their canonical capitalisation (`User-Agent`, `Accept`, `Content-Length`, `Content-Type`; a name with no canonical form is written capitalised after each hyphen) | 1700, 1701 |
| Removed | pseudo-fields, `Host` after the first, and the connection-specific fields `Connection`, `Keep-Alive`, `Proxy-Connection`, `Transfer-Encoding`, `Upgrade`, `HTTP2-Settings` and `TE` unless `trailers` | 1700 (the upgrade request's `Upgrade`, `HTTP2-Settings`, `Connection` are gone) |
| Cookie | HTTP/2 and HTTP/3 split `cookie` fields are joined with `; ` into one `Cookie:` | **unpinned** (nghttpx manual) |
| Added, last | `X-Forwarded-Proto: http` or `https` (the frontend's), then `Via: 1.1 nghttpx`, each appended to an existing one with `, ` for `Via` | 1700, 2500 strip exactly these two lines; no `X-Forwarded-For` (nghttpx's default is off) |
| Body | `Content-Length` as the client sent it. With a body and no `Content-Length`, `Transfer-Encoding: chunked` is written and the body is chunked | 1701 (`Content-Length: 10`, `datatosend`); chunked is **unpinned** |

The HTTP/1.1 request an `Upgrade: h2c` client sent (test 1701: `POST` with `Content-Length: 10`
and the body) is stream 1 with its body, which ADR-0091's own server cannot take; the gateway reads
the body by `Content-Length` before it answers `101`. An HTTP/1.1 request on the plaintext port with
no `Upgrade` and no HTTP/2 preface is forwarded as HTTP/1.1 under the same field rules and its reply
passed back as HTTP/1.1 (no case pins it; nghttpx does it).

**3. How the backend's reply becomes the response.**

- `:status` is the reply's status. Interim `1xx` replies are dropped (**unpinned**).
- Every field name is lower-cased (1700 `last-modified`, `etag`, `funny-head`).
- Dropped: `Connection`, `Keep-Alive`, `Proxy-Connection`, `Transfer-Encoding`, `Upgrade`
  (the reply's `Connection: close` is not in any `<stdout>`).
- `Content-Length` is kept as the backend sent it (1700 `content-length: 6`). A chunked reply is
  de-chunked and then has no `content-length`; a reply with neither is read to the backend's close
  and has none either. Neither is pinned.
- Order: the backend's remaining fields in order, then `server`, then `via`.
- **HTTP/2 writes `server: nghttpx`**, replacing the backend's `Server` (1700's backend sends
  `Server: test-server/fake`, which its `<stripfile>` `s/^server: nghttpx.*\r?\n//` then removes;
  2403's backend sends none and its `<stdout>` shows `server: nghttpx`). **HTTP/3 does not add it**:
  2503 has no `Server` in either the reply or `<stdout>` and no `<stripfile>`. HTTP/3 leaves a
  backend `Server` untouched (**unpinned**; 2500 strips the line either way).
- Both write `via: 1.1 nghttpx`, after an existing `via` as `<existing>, 1.1 nghttpx`
  (1700, 2403, 2500, 2503).
- No `date` is added: every runnable case's reply carries one.
- `HEAD`: `HEADERS` with `END_STREAM`, no `DATA`, `content-length` as the backend sent it (1702).
- **The end of the stream.** The stream ends when `Content-Length` bytes were relayed or the backend
  closed (the `http` half closes after its reply, as `swsclose` and `Connection: close` make it do;
  the gateway treats them alike). A close after a complete body ends the stream cleanly
  (`END_STREAM`, HTTP/3 FIN); the frontend connection stays open for the next stream (1700's second
  URL). A close before any reply byte is answered `502 Bad Gateway` with `content-type: text/html;
  charset=UTF-8`, `server: nghttpx` (HTTP/2) and `via`, and a short HTML body whose exact bytes are
  **unpinned**. A close in the middle of a body resets the stream, `RST_STREAM` `INTERNAL_ERROR` on
  HTTP/2 and a reset with `H3_INTERNAL_ERROR` on HTTP/3.

**4. Ports and certificates.**

| Port variable | Transport | Serves | Certificate |
| --- | --- | --- | --- |
| `%HTTP2PORT` | TCP, plaintext | HTTP/2 with prior knowledge, `Upgrade: h2c` (decision 2) and plain HTTP/1.1 | none |
| `%HTTP2TLSPORT` | TCP, TLS, ALPN `h2` only | HTTP/2 | generated `test-localhost`, issued by `test-ca` (BL-682, ADR-0080) |
| `%HTTP3PORT` | UDP, QUIC, ALPN `h3` | HTTP/3 | generated `test-localhost` |

All three bind `127.0.0.1` on port 0 and publish the bound port to the case variables. ALPN
`http/1.1` over TLS is not offered (no case needs it; **unpinned**). Test 2403 uses `--insecure`, 2500 and 2503
`--cacert test-ca.crt` and `--resolve localhost:%HTTP3PORT:%HOSTIP`, so the `localhost` name
validates against the generated certificate.

**5. Types and where they live** (all in `Protocols/Http/Surl.Protocol.Http.UnitLibrary`, with tests
in its `.UnitTests`, held to the library's 100% gates; none is composed by `Surl.Console`):

| Type | Role | Task |
| --- | --- | --- |
| `NghttpxGatewayRequestTranslator` | decision 2: an HTTP/2 or HTTP/3 request head and body to HTTP/1.1 request bytes | BL-1114 |
| `NghttpxGatewayResponseTranslator` | decision 3: the backend's reply bytes to response fields and body, per version | BL-1114 |
| `Http2GatewayUpstreamTestCaseServer` | HTTP/2 over TLS and prior knowledge, per-stream forwarding through an `IOriginConnector` | BL-1115 |
| `Http2GatewayCleartextUpgrade` | decision 2's `Upgrade: h2c` and the plain HTTP/1.1 passthrough, reusing `Http2CleartextUpgrade` | BL-1116 |
| `Http3GatewayUpstreamTestCaseServer` | HTTP/3 request streams through `IHttp3RequestStreamHandler` | BL-1117 |

BL-1118 and BL-1119 (in `Surl.Conformance.UnitLibrary` and `.UnitTests`) compose them; the names are
planning names and a task may rename one for a clearer one, recording it in its Notes.

**6. Selection rules and server commands.**

- `UpstreamTestCaseServers.All`: `http/2` and `http/3` become `IsBuilt: true`, protocol `http`, with
  no extra `PortVariable` (cases carry `%HTTP2PORT`, `%HTTP2TLSPORT` and `%HTTP3PORT` themselves,
  already in `UpstreamTestCaseVariables`). BL-1118 flips `http/2`, BL-1119 `http/3`.
- Features: `UpstreamCurlBuildFeatures.For` adds **`nghttpx`** when the pin has `http/2` (BL-1118)
  and **`nghttpx-h3`** when it has `http/3` (BL-1119), because the gateway half stands in for
  upstream's nghttpx: runtests sets the two when it finds nghttpx, and Surl's half is always found.
  A pin without HTTP2 or HTTP3 still skips `feature-missing:http/2` or `:http/3`, never
  `server-not-built`. Without BL-1119, 2500 and 2503 skip `feature-missing:nghttpx-h3`.
- `UpstreamTestCaseServerCommands`: both names get the `Http` set. The gateway reads no command; the
  `http` half it forwards to does, so `Testnum`, `swsclose` and the rest work as for `http`.

**7. Stand-in note.** The gateway halves are composed only by `Surl.Conformance.UnitTests`
(ADR-0080 decision 2: `Surl.Console` never composes a case seam). They use no shared store,
publish nothing on an instance's event stream and have no fault rules; they exist for upstream's
cases, not for a test suite's stand-in. `Surl.Api`'s own HTTP/2 and HTTP/3 servers (ADR-0081, 0082,
0091) are the stand-ins and are unchanged.

**The six runnable cases and what each expects on the `linux-x64` and `osx-arm64` pins:**

| Case | Needs | Expected | Rule that makes it pass |
| --- | --- | --- | --- |
| 1700 | `h2c` | pass: `101`, two responses on one connection; `<protocol>` two origin-form `GET`s | 1, 2, 3, and the correction above |
| 1701 | `h2c` | pass: forwarded `POST` carries `datatosend` and `Content-Length: 10` | 2 (body read before `101`) |
| 1702 | `h2c` | pass: `HEAD`, stream 1 has no `DATA` | 3 (`HEAD`) |
| 2403 | `http/2 SSL headers-api` | pass: `server: nghttpx` then `via`, and `header_json` lists both | 3 (HTTP/2 writes `server`) |
| 2500 | `http/3 nghttpx-h3` | pass: absolute-form `GET https://localhost:%HTTP3PORT/2500` forwarded | 2 (HTTP/3 absolute form) |
| 2503 | `http/3 nghttpx-h3 headers-api` | pass: no `server` field, `via` last | 3 (HTTP/3 adds no `server`) |

On `win-x64` each skips `feature-missing:` (`h2c` or `http/2` or `http/3`). 2500's and 2503's
`<limits>` (allocations) are not checked, as for every case. If a pin's run disagrees with a row, the
case fails and BL-1121 records it; the fix is to the half, not to this table, unless the case itself
shows the rule wrong, in which case this ADR gains an amendment.

## Consequences

- BL-1114 to BL-1119 build from this ADR with no further question. BL-1116's Context and criteria
  are corrected for test 1700; BL-1114, BL-1115, BL-1117, BL-1118 and BL-1119 keep theirs.
- FR-053 and the Phase 6 milestone's "No upstream test case runs over HTTP/2 or HTTP/3" stays true
  until BL-1118 lands; BL-1121 updates it with the recorded run.
- The unpinned rules are a risk to fidelity only for cases that do not exist at this tag; a later
  tag's case that pins one (a chunked backend, a `502`) amends decisions 2 and 3.
- No Curl port output is used: every expected byte is a case's own `<protocol>` or `<stdout>`
  (ADR-0003).
