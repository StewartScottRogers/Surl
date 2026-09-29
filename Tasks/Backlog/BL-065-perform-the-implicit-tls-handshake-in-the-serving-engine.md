---
id: BL-065
title: Perform the implicit TLS handshake in the serving engine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-006, BL-025]
touches: [Surl.Core.UnitLibrary, Surl.Core.UnitTests]
requirement: FR-021
created: 2026-09-28
completed:
---
# BL-065 — Perform the implicit TLS handshake in the serving engine

## Goal

For a listen URL whose scheme `TlsSchemes.IsImplicitTls` names, `ServingEngine` secures
each accepted connection with `UpgradeToTlsAsync` before handing it to the protocol
server, notes the handshake's outcome in the exchange log, and never serves a connection
whose handshake failed.

## Context

- Specification: `Documentation/Planning/Decisions/ADR-0010-the-server-side-tls-contract.md`,
  section 2 ("Who performs which handshake") and section 1 (`IConnection.TlsSession`,
  `UpgradeToTlsAsync`, `TlsSession`, `TlsHandshakeException`, `TlsSchemes`). ADR-0006
  section 4: the handshake runs inside the head timeout; section 5: a connection past a
  connection limit is closed without a handshake.
- Dependencies: BL-006 adds the TLS contract and the `InMemoryConnection` upgrade
  options (initial session, session handed out on upgrade, make-the-upgrade-throw,
  `UpgradeRequested`) to `Surl.Protocol.Abstractions`; BL-025 adds the engine's
  connection-limit check, which the handshake must follow.
- Where the work lands, in `Surl.Core.UnitLibrary`:
  - `ServingEngine.cs`, `ServeAndLogExchangeAsync`: after the `Exchange <id> opened` note
    and after BL-025's connection-limit check has let the connection through, when
    `TlsSchemes.IsImplicitTls(listenUrl.Scheme)`, call `UpgradeToTlsAsync` on the
    `RecordingConnection` (so the log records plaintext on both sides) with a head-timeout
    token: a `CancellationTokenSource` made with the engine's `TimeProvider` that cancels
    after `context.Limits.HeadTimeout`, linked to the exchange's cancellation token. Only
    after a completed handshake call `server.ServeAsync`. A plaintext scheme (`http`)
    never calls `UpgradeToTlsAsync`.
  - `RecordingConnection.cs`: forward `TlsSession` and `UpgradeToTlsAsync` to the wrapped
    connection.
- Notes, written with `log.Note`:
  - Completed: `TLS handshake completed: <Protocol>, <CipherSuite>, ALPN <ApplicationProtocol or none>`,
    each value the `TlsSession` member's `ToString()` (enum names, e.g.
    `TLS handshake completed: Tls13, TLS_AES_128_GCM_SHA256, ALPN http/1.1`), `none` when
    `ApplicationProtocol` is `null`.
  - Failed: `TLS handshake failed: <exception message>` for a `TlsHandshakeException`;
    for the head-timeout token firing (an `OperationCanceledException` while the
    exchange's own token is not cancelled), `TLS handshake failed: no handshake within
    the head timeout of <seconds> s`, `<seconds>` being `HeadTimeout.TotalSeconds` in
    invariant culture. Cancellation by shutdown keeps the existing `Exchange <id>
    cancelled at shutdown.` note.
  - A failed implicit handshake: note it, write nothing, never call `ServeAsync`, dispose
    the connection; the `Exchange <id> ended; closing the connection.` note still follows.
  - A `TlsHandshakeException` escaping `ServeAsync` (a server's own `STARTTLS`/`AUTH TLS`
    upgrade failing) is noted `TLS handshake failed: <message>`, not
    `... the protocol server threw ...` (`NoteHowTheExchangeEnded`).
  - A failed handshake has no exit code and never ends the process (ADR-0010 section 2).
- Tests go in `Surl.Core.UnitTests/ServingEngineTests.cs` and
  `RecordingConnectionTests.cs`, with the existing fakes (`FakeListenerFactory`,
  `FakeConnectionListener`, `FakeProtocolServers`, `FakeExchangeLogFactory`,
  `ManualTimeProvider`) and `InMemoryConnection`. No socket, no `SslStream`, no
  `Thread.Sleep`, no package.

## Acceptance criteria

- [ ] A `ServingEngineTests` test serves an `https` listen URL: the connection's
      `UpgradeRequested` is true, the fake server receives a connection whose
      `TlsSession` is the session `InMemoryConnection` handed out, and the log holds the
      `TLS handshake completed: ...` note with `ALPN none` for a session with no ALPN and
      `ALPN http/1.1` for one with it.
- [ ] A test serves an `http` listen URL and asserts `UpgradeRequested` is false and the
      server's connection has a `null` `TlsSession`.
- [ ] A test makes the upgrade throw `TlsHandshakeException`: `ServeAsync` of the fake
      server is never called, nothing is written to the connection, the connection is
      disposed, the log holds `TLS handshake failed: <message>` and no
      `protocol server threw` note, and the engine keeps accepting (a second connection
      on the same listener is served).
- [ ] A test holds the handshake pending, advances `ManualTimeProvider` past
      `ExchangeLimits.Default.HeadTimeout` (not before: a check just under it shows the
      handshake still pending), and asserts the `no handshake within the head timeout`
      note and that the server is never called.
- [ ] A test with a connection past BL-025's connection limit on an `https` listener
      asserts `UpgradeRequested` is false.
- [ ] A test has the fake server throw `TlsHandshakeException` from `ServeAsync` and
      asserts the `TLS handshake failed: <message>` note in place of the
      `protocol server threw` note.
- [ ] `RecordingConnectionTests` pin that `TlsSession` and `UpgradeToTlsAsync` forward to
      the wrapped connection, and that bytes read and written after the upgrade are still
      recorded.
- [ ] `dotnet build Surl.Core.UnitLibrary -warnaserror` is clean, the fast tests
      (`dotnet test --filter "TestCategory!=Integration"`) are green, and
      `powershell -NoProfile -File Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Core.UnitLibrary`.

## Notes

BL-006 already requires every existing `IConnection` implementation to gain the two
members, so `RecordingConnection` may already forward them when this task starts; if so,
the forwarding criterion is met by adding or confirming its tests.

## Log

- 2026-09-28: Created.
