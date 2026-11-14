---
id: BL-1146
title: Add Surl.Tls13Stream, a Stream that runs the server side of TLS 1.3 over a transport
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1145]
touches: [Cryptography/Surl.Tls13Stream.UnitLibrary, Cryptography/Surl.Tls13Stream.UnitTests, Surl.slnx]
requirement: FR-053
created: 2026-11-14
completed: 2026-11-14
---
# BL-1146 — Add Surl.Tls13Stream, a Stream that runs the server side of TLS 1.3 over a transport

## Goal

A `Tls13ServerStream` completes a TLS 1.3 server handshake over any `Stream` and then carries application data, in a new `Surl.Tls13Stream.UnitLibrary` with its `.UnitTests`.

## Context

- ADR-0157 decision 2, second bullet and decision 3. Mirror `Surl.TlsSrp`'s `TlsSrpStream` for shape. Use the `new-project` skill; the library sits in `Cryptography/`.
- Drives `TlsServerHandshake` from `Surl.Tls13` and BL-1145's record layer. Accepts already-read ClientHello bytes to replay.
- Handles: middlebox `change_cipher_spec` ignored, `close_notify` answered, fatal alerts written then raised, `KeyUpdate` answered, plaintext record after the handshake refused, ALPN from the offered list, no tickets, no client certificate, no 0-RTT.

## Acceptance criteria

- [x] The project and its test project exist in `Surl.slnx` under `/Cryptography/`, gated at 100% line and branch coverage and complexity at most 10.
- [x] A unit test completes a handshake with a scripted in-memory client built from RFC 8448 values and exchanges application data both ways.
- [x] Tests cover `close_notify`, a fatal alert, `KeyUpdate`, a bad client Finished and a truncated record.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `Tls13ServerStream.AcceptAsync(transport, options, random, alreadyRead, ct)` in `Surl.Tls13Stream.UnitLibrary`; 44 tests, 100% line and branch coverage by Measure-CodeQuality, no method over complexity 1-4.
- Decisions: the stream owns the transport only once AcceptAsync returns; a client `close_notify` is answered with ours; any alert other than `close_notify` and `user_canceled` is fatal (RFC 8446 section 6); the compatibility `change_cipher_spec` goes after the first ServerHello or HelloRetryRequest only when the ClientHello has a non-empty session id (read from the raw bytes, because `TlsServerHandshake.ClientHello` is null after a HelloRetryRequest until the second hello).
- The scripted client derives the key schedule itself from RFC 8448 section 3 values (ClientHello, x25519 keys, ServerHello reproduced byte for byte); only record protection is the library's, already pinned to RFC 8448 by BL-1145.
- Follow-up outside `touches`: `ProtocolIsolationTests` (Foundation/Surl.Protocol.Abstractions.UnitTests) has no row for `Surl.Tls13Stream.UnitLibrary` yet, so its references are not policed; add `[Tls13Stream] = [Tls13]`.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. Tls13ServerStream completes a TLS 1.3 server handshake over any Stream (RFC 8448 client) and carries application data, close_notify, KeyUpdate and alerts; 100% coverage
