---
id: BL-066
title: Answer gophers (Gopher over TLS) in Surl.Protocol.Gopher
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-034, BL-006]
touches: [Surl.Protocol.Gopher.UnitLibrary, Surl.Protocol.Gopher.UnitTests, Record-CurlExchange.ps1, Surl.Console.UnitTests, Surl.Console/CLAUDE.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
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

- [x] `GopherProtocolServer.Schemes` equals `["gopher", "gophers"]` in that order, pinned
      by a test in `GopherProtocolServerTests` that replaces `Schemes_IsGopherOnly` and
      is named for what it now pins.
- [x] `Record-CurlExchange.ps1 -Raw -Tls` records an exchange over TLS with decrypted
      `request.bin` and `transcript.txt`. The script's `.PARAMETER Raw` help no longer
      lists `-Tls` as refused, and `.PARAMETER Tls` states it applies to `-Raw`.
- [x] `Fixtures/gophers-file-selector` (curl sent `/file.txt` CRLF) and
      `Fixtures/gophers-root-menu` (curl sent the empty selector, CRLF) are recorded from
      the pinned upstream curl 8.21.0 build. Each has `exitcode.txt` 0 and an empty
      `stderr.txt`, and each has a row in `Fixtures/README.md` giving its exact command
      line.
- [x] `GopherProtocolServerTests` replays each new fixture's `request.bin` through an
      `InMemoryConnection` standing in for a secured connection, with listen URL
      `gophers://127.0.0.1:18634/`. The test asserts that the server's reply equals that
      fixture's `stdout.bin` byte for byte. The root menu names host `127.0.0.1` and
      port `18634`.
- [x] `dotnet build Surl.Protocol.Gopher.UnitLibrary -warnaserror` and
      `dotnet build Surl.Protocol.Gopher.UnitTests -warnaserror` are clean.
- [x] `dotnet test --filter "TestCategory!=Integration"` passes, and no new test needs
      `TestCategory=Integration`.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Protocol.Gopher.UnitLibrary`: 100% line and branch coverage, complexity at
      most 10, and CRAP at most 30.
- [x] `Surl.Protocol.Gopher.UnitLibrary/CLAUDE.md` and the XML doc comment on `Schemes`
      state that the server answers both `gopher` and `gophers`, and that TLS comes from
      the engine (ADR-0010).

## Notes

- Wiring `gophers` into `surl`'s listener and the live conformance run belong with BL-040
  and the TLS tasks (BL-012, BL-065), not here.
- Plan (run in-session; a one-line scheme change, a recorder extension and two replay
  tests): `Schemes` becomes `["gopher", "gophers"]`; the selector, file and menu logic is
  untouched, since the engine hands over a connection that already carries plaintext.
- Recorder: `-Raw -Tls` wraps the accepted connection in an `SslStream` (TLS 1.2, the
  `-Tls` throwaway certificate). An `SslStream` can hold decrypted bytes the socket's
  `Poll` cannot see, so over TLS a burst ends when a `ReadAsync` has not completed within
  `RawIdleMilliseconds`; the pending read carries over to the next burst. The server sends
  a close_notify (`ShutdownAsync`) before closing unless curl hung up first, and writes
  "= TLS handshake completed" at the top of the transcript. Default taken: TLS 1.2 only,
  as the other session modes serve; `-TlsRenegotiationOff` is refused with `-Raw` (its
  relay would wrap TLS twice). The plain `-Raw` path is unchanged: re-recording
  `file-selector` reproduced all five files byte for byte. A handshake curl refuses
  (no `-k`: exit 60, SEC_E_UNTRUSTED_ROOT) records an empty `request.bin` and transcript.
- Touches widened (no task in Doing named them): `Surl.Console.UnitTests`, because
  `CommandLineRunnerTests` pins `surl --version`'s protocol list, which is built from every
  registered server's `Schemes` and now reads `dict gopher gophers http https mqtt telnet
  tftp`; and `Surl.Console/CLAUDE.md`, whose server list would otherwise be false. The
  engine already performs the implicit handshake for every `TlsSchemes.IsImplicitTls`
  scheme and `ServerTlsComposition` builds a certificate for any of them, so `surl`
  accepts a `gophers://` listen URL from this change on. Gopher declares `gophers` itself
  (as this task's goal and ADR-0012 say), so Console needs no `ImplicitTlsSchemeServer`
  for it.
- Gates: Gopher 68 tests, Console 63, whole fast run green; `Measure-CodeQuality.ps1`
  reports 0 failing members, Gopher 100% line and branch, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. GopherProtocolServer answers gophers as well as gopher, proven by two -Raw -Tls recordings from pinned upstream curl 8.21.0
