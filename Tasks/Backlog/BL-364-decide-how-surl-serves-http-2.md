---
id: BL-364
title: Decide how Surl serves HTTP/2
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-362]
touches: [Documentation/Planning/Decisions, Documentation/Product/Requirements.md, Documentation/Wiki/Glossary.md, UpstreamCurlBuilds.json]
requirement: none
created: 2026-11-04
completed:
---
# BL-364 — Decide how Surl serves HTTP/2

## Goal

An ADR decides how Surl serves HTTP/2 (RFC 9113) to pinned upstream curl, on `https` through ALPN and on
`http` through prior knowledge and the `Upgrade: h2c` request. Its decisions rest on what pinned upstream
curl was measured to send, and the HTTP/2 tasks BL-365 to BL-371 build what it decides.

## Context

- Scope: the product overview, "Also in scope" ("HTTP versions: 1.0, 1.1, 2 and 3 over QUIC, on the
  server side") and Phasing row 6. `Protocols/Http/Surl.Protocol.Http.UnitLibrary/CLAUDE.md` says "hand-built HTTP/2
  and HTTP/3". The BCL has no public HTTP/2 server framing and no HPACK, so both are hand-built (root
  `CLAUDE.md`, "Decisions").
- ADR-0010 section 4: `https` and `wss` offer exactly ALPN `http/1.1` today
  (`Foundation/Surl.Networking.UnitLibrary/TlsApplicationProtocols.cs`), and "`h2` joins the list with the HTTP/2
  server, in the ADR that task records".
- What upstream curl 8.21.0 does (tag `curl-8_21_0`, `docs/cmdline-opts/http2.md` and
  `http2-prior-knowledge.md`):
  - For HTTPS, curl negotiates HTTP/2 in the TLS handshake by default.
  - For HTTP, `--http2` tries `Upgrade:`.
  - `--http2-prior-knowledge` speaks HTTP/2 directly with no Upgrade, and on HTTPS since 8.10.0 it
    offers only `h2`.

  So once `h2` is offered, the `linux-x64` and `osx-arm64` reference builds switch every `https`
  conformance test to HTTP/2. Some of those tests pin HTTP/1.1 output (`-i`, `-v`). The ADR decides
  how those tests and the default offer change.
- Builds with `HTTP2`: the Linux and macOS reference builds. On Windows, only the supplementary builds
  have it: curl.se's 8.22.0_2, which ADR-0017 decision 3 names for HTTP/2 and HTTP/3, and
  stunnel/static-curl's 8.21.0, the reference release, pinned by ADR-0030, ADR-0042 and ADR-0063 for
  other uses.
- Measure first, with BL-362's `-TlsAlpn` together with `-Raw` for `h2`, `-Raw` for prior knowledge,
  and the default HTTP mode for the Upgrade request. Linux and macOS measurements can come from a CI
  run, as BL-352 did.
- Limits: ADR-0006 section 1 (`--max-message` covers "HTTP/2 and HTTP/3 header block"; the other
  limits) and section 5 ("Framed message too long": the protocol's own error, then close).
- Logins bound to a connection: ADR-0041 (NTLM) and ADR-0044 (Negotiate), which RFC 9113 does not
  allow over HTTP/2.
- The answering code: `HttpRequestResponder` (`Surl.Protocol.Http.UnitLibrary`) writes HTTP/1.x to
  an `IConnection`. Shared head parsing lives in `Surl.HttpMessage` (ADR-0070).
- Project rules: ADR-0002 decision 3's table of horizontal libraries, and the
  `ProtocolIsolationTests` in `Surl.Protocol.Abstractions.UnitTests`.

Decide and record, with each measured fact as a table row (command, build SHA-256, exit code, bytes):

1. Which pinned build measures HTTP/2 on Windows. If it is not 8.22.0_2, amend ADR-0017 decision 3
   and the `origin` text of the affected entries in `UpstreamCurlBuilds.json`.
2. The ALPN list offered for `https` (and whether `wss` changes), whether any option turns HTTP/2
   off, and how the existing `https` conformance tests on Linux and macOS change.
3. h2c: prior knowledge (how an `http` listener recognises the preface) and the `Upgrade: h2c`
   response (`101`, `HTTP2-Settings`).
4. Where the code lives. Planning assumptions: HPACK in a new horizontal library `Surl.Hpack` (BL-365)
   that later holds the Huffman code QPACK reuses; frames and the connection in
   `Surl.Protocol.Http`. Amend ADR-0002's table to match.
5. The `SETTINGS` the server sends, and how each ADR-0006 limit maps onto HTTP/2: header block,
   concurrent streams, flow-control windows, frame size, idle and head timeouts. Name the error code
   each limit ends with (`GOAWAY` or `RST_STREAM`).
6. What upstream curl sends and needs (`SETTINGS`, `WINDOW_UPDATE`, `HEADERS`/`CONTINUATION`,
   `PRIORITY`, `PING`), and that server push stays off.
7. How the content store, every authentication scheme (NTLM and Negotiate included), `100 Continue`,
   uploads, the lingering close and the log lines carry over to HTTP/2.
8. The version-independent answering shape that BL-366 splits out of `HttpRequestResponder`.
9. WebSocket over HTTP/2 (RFC 8441): whether upstream curl 8.21.0 asks for it.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked
      "Decided by Claude under Stewart's delegation". It answers decisions 1 to 9, with measured tables
      for prior knowledge, the Upgrade request and `h2` over TLS, and is listed in that folder's
      `README.md`.
- [ ] ADR-0010 section 4 and ADR-0002's table are amended, or point to the new ADR, so that neither
      states something the new ADR changes.
- [ ] `Documentation/Product/Requirements.md` has a functional requirement row for HTTP/2, in the
      shape of FR-048, citing the ADR and the build it was measured with.
- [ ] `Documentation/Wiki/Glossary.md` defines the terms the ADR introduces (for example HPACK, h2c,
      connection preface, stream).

## Notes

## Log

- 2026-11-04: Created.
