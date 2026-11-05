---
id: BL-734
title: Let a protocol server peek at a connection's next byte before a TLS upgrade
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-524]
touches: [Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-734 — Let a protocol server peek at a connection's next byte before a TLS upgrade

## Goal

`IConnection` gains a peek: a protocol server can wait up to a given time for the client's next byte and learn it
(or learn that none came) without consuming it, so a later `ReadAsync` or `UpgradeToTlsAsync` still sees that byte.

## Context

- Decision: ADR-0113 decision 6 (BL-524). On `natss` the NATS server waits 50 ms for the client's first byte: a
  TLS-first client (`handshake_first`) has sent a ClientHello, so the server runs the handshake before INFO;
  otherwise it sends INFO with `tls_required` and then peeks again: `0x16` (a TLS handshake record) means the
  client is starting TLS and the server calls `UpgradeToTlsAsync`; any other byte is a plain-text CONNECT, answered
  `-ERR 'Secure Connection - TLS Required'`. Both need the byte left unread.
- Planning shape (the implementer may refine it): `ValueTask<byte?> PeekByteAsync(TimeSpan wait, CancellationToken)`
  on `IConnection`, `null` when nothing arrived within `wait` or the peer half-closed. `InMemoryConnection` gets
  the same, so protocol tests stay off the network (ADR-0004). `Surl.Networking`'s socket connection keeps the
  peeked bytes for the next read and hands them to the TLS handshake ahead of the socket's (a prefix stream under
  the `SslStream`); waiting runs on the injected `TimeProvider`.
- Only `Surl.Networking.UnitLibrary` constructs sockets and `SslStream` (root `CLAUDE.md`).

## Acceptance criteria

- [ ] Named tests show, on `InMemoryConnection` and on the socket connection: a peek returning the next byte and the
      following `ReadAsync` still returning it; a peek returning `null` after the wait with nothing sent; a peek
      followed by `UpgradeToTlsAsync` completing a handshake whose first byte was peeked.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

- Filed by BL-524 (lane 2, 2026-11-05): ADR-0113 decision 6 needs it for BL-527.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
