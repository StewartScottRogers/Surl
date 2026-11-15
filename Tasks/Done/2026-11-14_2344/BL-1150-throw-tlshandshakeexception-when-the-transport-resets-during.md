---
id: BL-1150
title: Throw TlsHandshakeException when the transport resets during the hand-built TLS 1.3 handshake
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-14
completed: 2026-11-14
---
# BL-1150 — Throw TlsHandshakeException when the transport resets during the hand-built TLS 1.3 handshake

## Goal

On every platform, a transport that fails or resets while `ServerTlsHandshake` reads the first
record for the hand-built TLS 1.3 path (`ClientHelloPeek`) is a `TlsHandshakeException` wrapping
the `IOException`, as the SslStream, TLS-SRP and `Tls13ServerStream` paths already throw.

## Context

CI run 43651751537 (PR #36, frozen branch `factory/merge-20261114-131117`, commit 145f16b) failed
only on "Build and test (macos-latest)":
`StreamConnectionTlsTests.UpgradeToTlsAsync_TransportResetDuringHandshake_ThrowsTlsHandshakeException`
(`Foundation/Surl.Networking.UnitTests/StreamConnectionTlsTests.cs:168`):
"Expected exception of exact type TlsHandshakeException but caught IOException: The peer reset
the connection", thrown from `ClientHelloPeek.ReadAsync` (`ClientHelloPeek.cs:47`) via
`ServerTlsHandshake.AuthenticateTls13OnlyWithHandBuiltStreamAsync`. This is a real defect from
BL-1147, not a flake. That holds the merge of #36 to `master`.

- `Foundation/Surl.Networking.UnitLibrary/ServerTlsHandshake.cs`: `AuthenticateAsync` picks
  `AuthenticateTls13OnlyWithHandBuiltStreamAsync` only where `ITls13Support.SslStreamLacksTls13Server`
  (`PlatformTls13Support`: macOS only), which is why Windows and Linux pass. That method awaits
  `ClientHelloPeek.ReadAsync` outside any `try`, so an `IOException` from the peek escapes
  unwrapped. `AuthenticateTls13Async` and the other paths catch `IOException` and throw
  `TlsHandshakeException`.
- The project's `CLAUDE.md` states the contract: "a failed handshake is `TlsHandshakeException`".

## Acceptance criteria

- [x] An `IOException` from `ClientHelloPeek.ReadAsync` in `AuthenticateTls13OnlyWithHandBuiltStreamAsync` is thrown as `TlsHandshakeException` with that `IOException` as `InnerException`. Cancellation still surfaces as `OperationCanceledException`.
- [x] A new fast test, with a fake `ITls13Support` saying `SslStreamLacksTls13Server` and a transport that throws `IOException` on read, fails before the fix and passes after it on Windows. It names the hand-built path in its method name.
- [x] `UpgradeToTlsAsync_TransportResetDuringHandshake_ThrowsTlsHandshakeException` is unchanged and passes.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green. Surl.Networking.UnitLibrary keeps 100% line and branch coverage.

## Notes

Filed by an interactive session on 2026-11-14 after PR #36's macOS job failed. The fix is small:
put the peek inside the same `try` / `catch (IOException)` the other paths use.

Done: wrapped only the peek in `catch (IOException)` -> `TlsHandshakeException` (the later paths already wrap their own). Test: `AuthenticateAsync_TransportResetWhileReadingTheHelloOnTheHandBuiltPath_ThrowsTlsHandshakeException`. Networking fast tests: 501 passed.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. A transport reset while peeking the TLS 1.3 hello is a TlsHandshakeException on every platform
