---
id: BL-370
title: Offer h2 by ALPN on https and serve HTTP/2 over TLS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-368]
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests, Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests, Conformance/Surl.Conformance.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-370 — Offer h2 by ALPN on https and serve HTTP/2 over TLS

## Goal

`surl https://` offers the ALPN list that BL-364's ADR decides, `h2` included, and serves HTTP/2 on
every TLS connection where `h2` was agreed. A connection that agreed `http/1.1`, or no ALPN at all,
is served as it is today.

## Context

- Decision: BL-364's ADR, decision 2. That decision covers the list, the option that turns HTTP/2
  off (if there is one), and what happens to `wss`.
- `Foundation/Surl.Networking.UnitLibrary/TlsApplicationProtocols.cs` offers `http/1.1` for `https` and `wss`.
  `TlsApplicationProtocolsTests` pins that. ADR-0010 section 4 is amended by BL-364.
- The HTTP server reads the agreed protocol from `TlsSession.ApplicationProtocol`
  (`Foundation/Surl.Protocol.Abstractions.UnitLibrary/TlsSession.cs`, ADR-0010) and picks HTTP/2 (BL-368's
  connection) or HTTP/1.x.
- Once `h2` is offered, the Linux and macOS reference builds negotiate HTTP/2 for every `https` test
  in `Surl.Conformance.UnitTests` (curl negotiates HTTP/2 for HTTPS by default:
  `docs/cmdline-opts/http2.md`, tag `curl-8_21_0`). Adapt the tests that pin HTTP/1.1 output, as the
  ADR says, in this same diff. Otherwise CI's Linux and macOS legs go red. Lanes test only on Windows,
  where the reference build has no `HTTP2`, so read every `https` conformance test for this.
- If a new option is added, it needs its `--aihelp` facts and topic pages (root `CLAUDE.md`).

## Acceptance criteria

- [x] `TlsApplicationProtocolsTests` pins the ADR's list for `https` and `wss`, and the list for every
      other scheme is unchanged.
- [x] An `InMemoryConnection` test with `ApplicationProtocol` `h2` is served as HTTP/2, and tests with
      `http/1.1` and with `null` are served as HTTP/1.1 with unchanged bytes.
- [x] Every `https` test in `Surl.Conformance.UnitTests` that pins HTTP/1.1 output either pins the
      HTTP version it asks for (for example `--http1.1`) or pins the HTTP/2 output, as the ADR
      decides. Notes list each test changed.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, both libraries keep 100% line
      and branch coverage, and `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~OverHttps"`
      passes on Windows.

## Notes

- Plan (ADR-0081 decision 2; no new ADR needed): `TlsApplicationProtocols` offers `[h2, http/1.1]`
  for `https` and keeps `[http/1.1]` for `wss`. `HttpProtocolServer.ServeAsync` sends a TLS
  connection that agreed `http/1.1` or no ALPN straight to `Http1ConnectionServer` as before; one
  that agreed `h2` takes the cleartext path (preface read within the head timeout, then
  `Http2ConnectionServer`, or HTTP/1.x with the bytes given back), except that its HTTP/1.x handler
  is the answerer itself, not `Http2CleartextUpgradeHandler`: `Upgrade: h2c` is cleartext only
  (ADR-0081 decision 3). No surl option was added (ADR-0081 rejects one), so no `--aihelp` option
  facts; the `http` topic's prose now says what `https` offers and serves.
- Measured live on Windows on 2026-11-04 with the pinned static 8.21.0 build (SHA-256
  `589C8E4D...`) against `surl --self-signed https://127.0.0.1:18443/`: `curl -k -i` gets
  `HTTP/2 200` and the file, exit 0; `--http1.1` gets `HTTP/1.1 200 OK`; two URLs with
  `-w %{http_version}` print `2` twice; a missing file with `-f` exits 22. The Git for Windows
  reference build (Schannel) gets `HTTP/1.1 200 OK`.
- Conformance tests: none of the existing `https` tests pinned HTTP/1.x output. Every one in
  `UpstreamCurlFetchesFromSurlOverHttpsTests`, and the `https` ones in
  `UpstreamCurlLogsInToSurlOverHttpTests` (Basic, Bearer), pins only the exit code, the body and a
  status in stderr, so per ADR-0081 decision 2 they stay as they are (HTTP/2 on Linux and macOS,
  HTTP/1.1 on Windows). Added to `UpstreamCurlFetchesFromSurlOverHttpsTests`:
  `GetWithHttp11_ThrowawayCertificate_IsServedOverHttp11` (every platform),
  `Get_ThrowawayCertificate_IsServedOverHttp2` (not Windows) and
  `Get_ThrowawayCertificate_IsServedOverHttp11ToTheSchannelBuild` (Windows). `wss` is unchanged.
- NTLM and Negotiate on an `https` HTTP/2 connection are still answered over HTTP/2, not reset
  with `HTTP_1_1_REQUIRED`; that is BL-602. No current conformance test runs either over `https`.
- Coverage: `Surl.Networking.UnitLibrary` and `Surl.Protocol.Http.UnitLibrary` are at 100% line and
  branch with 0 failing members. `Surl.Cli.UnitLibrary` has two failing members in code this task
  did not touch (left by BL-575); filed as BL-609.
- `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~OverHttps"` on
  Windows: 12 passed, 1 skipped (the HTTP/2 test, which is excluded on Windows).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. surl https:// offers h2 then http/1.1 by ALPN and serves HTTP/2 on connections that agree h2
