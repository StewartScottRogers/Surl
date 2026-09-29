---
id: BL-066
title: Answer gophers (Gopher over TLS) in Surl.Protocol.Gopher
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-034, BL-006]
touches: [Surl.Protocol.Gopher.UnitLibrary, Surl.Protocol.Gopher.UnitTests, Record-CurlExchange.ps1]
requirement: none
created: 2026-09-28
completed:
---
# BL-066 — Answer gophers (Gopher over TLS) in Surl.Protocol.Gopher

## Goal

`GopherProtocolServer.Schemes` gains `gophers`, and a `gophers` exchange is served over
implicit TLS exactly as `gopher` is (ADR-0012). Byte scripts recorded from the pinned
upstream curl 8.21.0 build with `Record-CurlExchange.ps1` prove it, never the Curl port
(ADR-0003).

## Context

- BL-034 declared only `gopher`: `Surl.Protocol.Gopher.UnitLibrary/GopherProtocolServer.cs`
  has `Schemes = ["gopher"]`, and `GopherProtocolServerTests.Schemes_IsGopherOnly` pins it.
- ADR-0002 "Consequences" says a secure variant is the same protocol server over a
  secured connection, never a second library. ADR-0010 lists `gophers` among the
  implicit-TLS schemes (`TlsSchemes.IsImplicitTls`). ADR-0012 "Consequences" says
  `gophers` joins `Schemes` with the TLS contract, in a later task. This is that task.
- ADR-0010 section 2: the engine performs the implicit handshake (BL-065 in `Surl.Core`,
  over BL-012's `SslStream` in `Surl.Networking`). The protocol server receives an
  `IConnection` that already carries plaintext, so the server's selector, file and menu
  logic does not change. Do not construct an `SslStream` or call `UpgradeToTlsAsync`
  here. This task depends on BL-006, which adds the TLS contract (`IConnection.TlsSession`,
  `TlsSession`, the `InMemoryConnection` session options) to
  `Surl.Protocol.Abstractions.UnitLibrary` and `Surl.Networking.UnitLibrary`. With it,
  the replay tests can stand in a secured connection.
- ADR-0012's menu rules apply unchanged for `gophers`. The host is the listen URL's host,
  with a wildcard replaced by the connection's local address. The port is the listen
  URL's bound port. The menu's lines name no scheme, and ADR-0012 gives no `gophers`
  default port, so none is added.
- Recording: `Record-CurlExchange.ps1 -Raw` currently refuses `-Tls` (see its
  `.PARAMETER Raw` help). Extend the script so that `-Raw -Tls` answers the connection
  over TLS with the same throwaway certificate `-Tls` already uses (curl needs `-k`).
  `request.bin` and `transcript.txt` must hold the decrypted bytes, and a failed
  handshake is recorded as an empty connection, as `-Tls` does today. Update the script's
  help to match. Run only the build pinned in `UpstreamCurlBuilds.json`.
- Existing fixtures and their recording commands are listed in
  `Surl.Protocol.Gopher.UnitTests/Fixtures/README.md`. Record the new cases the same way,
  with `-CurlArgs '-sS','-k','gophers://127.0.0.1:18634/...'`, into new folders such as
  `Fixtures/gophers-file-selector` and `Fixtures/gophers-root-menu`. Embed them as
  resources like the existing ones. The `.gitattributes` there keeps their CRLF bytes.

## Acceptance criteria

- [ ] `GopherProtocolServer.Schemes` equals `["gopher", "gophers"]` in that order, pinned
      by a test in `GopherProtocolServerTests` that replaces `Schemes_IsGopherOnly` and
      is named for what it now pins.
- [ ] `Record-CurlExchange.ps1 -Raw -Tls` records an exchange over TLS with decrypted
      `request.bin` and `transcript.txt`. The script's `.PARAMETER Raw` help no longer
      lists `-Tls` as refused, and `.PARAMETER Tls` states it applies to `-Raw`.
- [ ] `Fixtures/gophers-file-selector` (curl sent `/file.txt` CRLF) and
      `Fixtures/gophers-root-menu` (curl sent the empty selector, CRLF) are recorded from
      the pinned upstream curl 8.21.0 build. Each has `exitcode.txt` 0 and an empty
      `stderr.txt`, and each has a row in `Fixtures/README.md` giving its exact command
      line.
- [ ] `GopherProtocolServerTests` replays each new fixture's `request.bin` through an
      `InMemoryConnection` standing in for a secured connection, with listen URL
      `gophers://127.0.0.1:18634/`. The test asserts that the server's reply equals that
      fixture's `stdout.bin` byte for byte. The root menu names host `127.0.0.1` and
      port `18634`.
- [ ] `dotnet build Surl.Protocol.Gopher.UnitLibrary -warnaserror` and
      `dotnet build Surl.Protocol.Gopher.UnitTests -warnaserror` are clean.
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes, and no new test needs
      `TestCategory=Integration`.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Gopher.UnitLibrary`: 100% line and branch coverage, complexity at
      most 10, and CRAP at most 30.
- [ ] `Surl.Protocol.Gopher.UnitLibrary/CLAUDE.md` and the XML doc comment on `Schemes`
      state that the server answers both `gopher` and `gophers`, and that TLS comes from
      the engine (ADR-0010).

## Notes

- Wiring `gophers` into `surl`'s listener and the live conformance run belong with BL-040
  and the TLS tasks (BL-012, BL-065), not here.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
