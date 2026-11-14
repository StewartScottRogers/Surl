---
id: BL-1144
title: Decide how macOS serves TLS 1.3 so upstream test 4001 gets exit 101 (ECH required)
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1141]
touches: [Documentation/Planning/Decisions]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1144 — Decide how macOS serves TLS 1.3 so upstream test 4001 gets exit 101 (ECH required)

## Goal

An ADR records how Surl serves TLS 1.3 on macOS, so upstream test 4001 (`--ech ecl:... -k`, server ignores ECH) can end with curl exit 101 there as on Linux.

## Context

- CI run 43637240745 (head `ca55e264`): on `osx-arm64` only, curl exits 35 (`TLS connect error: ... tlsv1 alert protocol version`), expected 101; the server half logs `TLS handshake failed: Authentication failed`. Linux passes.
- Cause: ECH requires TLS 1.3, so curl offers only 1.3. .NET's `SslStream` server on macOS (Apple Secure Transport) negotiates up to TLS 1.2, so the handshake fails with a protocol-version alert; other macOS cases pass because curl falls back to 1.2. This is the platform's primitive limit, not a bug in the server half.
- CLAUDE.md's rule: nothing is left out because the BCL has no primitive. The decision to record: hand-build the TLS 1.3 server handshake (its own `Surl.<Area>.UnitLibrary` with `.UnitTests`, same gates; `Surl.TlsSrp` already exists for a related hand-built piece) and use it where `SslStream` cannot, or on every platform. Follow-up implementation tasks hang off the ADR.

## Acceptance criteria

- [ ] An ADR marked "Decided by Claude under Stewart's delegation" states the approach, the project that holds it and the ordered implementation tasks.
- [ ] Those implementation tasks are filed, and BL-1142's `depends-on` lists them.

## Notes

## Log

- 2026-11-14: Created.
