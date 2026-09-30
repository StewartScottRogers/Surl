---
id: BL-148
title: Decide the hand-built SSH primitive libraries and their rows in ADR-0002's reference table
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Documentation/Product/Product-Overview.md]
requirement: FR-039
created: 2026-09-29
completed: 2026-09-29
---
# BL-148 — Decide the hand-built SSH primitive libraries and their rows in ADR-0002's reference table

## Goal

An accepted ADR (the next free number, expected ADR-0048) settles which hand-built
cryptographic libraries the SSH server needs, what each holds and may reference, and adds
them to ADR-0002 decision 3's reference table, so BL-149 can create them and four lanes can
build them at once.

## Context

- Root `CLAUDE.md`, "Decisions": Curve25519, Ed25519 and ChaCha20-Poly1305 are built by hand,
  and each hand-built piece is isolated in its own `Surl.<Area>.UnitLibrary` with its own
  `.UnitTests`. ADR-0002 decision 3: a further hand-built library joins the reference table
  through a new ADR. `Surl.Cryptography.UnitLibrary/CLAUDE.md` says today that the SSH
  primitives would land in `Surl.Cryptography`, which already holds `Md4` and
  `Sha512Slash256` (BL-112, BL-119) and references nothing.
- The plan (BL-149 to BL-153, BL-167 to BL-169) already uses these four libraries; the ADR
  adopts them and records why:

  | Library | Holds | Specification | May reference |
  | --- | --- | --- | --- |
  | `Surl.Cryptography.Curve25519.UnitLibrary` | GF(2^255-19) field arithmetic and X25519 | RFC 7748 | nothing |
  | `Surl.Cryptography.Ed25519.UnitLibrary` | Ed25519 key generation, signing, verification | RFC 8032 section 5.1 | `Surl.Cryptography.Curve25519.UnitLibrary` (the field arithmetic) |
  | `Surl.Cryptography.ChaCha20.UnitLibrary` | the ChaCha20 block function and stream, 96-bit nonce (RFC 8439) and the 64-bit nonce and counter form OpenSSH's `chacha20-poly1305@openssh.com` uses | RFC 8439 sections 2.1 to 2.4; OpenSSH `PROTOCOL.chacha20poly1305` | nothing |
  | `Surl.Cryptography.Poly1305.UnitLibrary` | the Poly1305 one-time authenticator | RFC 8439 section 2.5 | nothing |

  Separate projects are what let the dark factory run them in parallel lanes (a task's
  `touches` names a project); merging them would serialise them.
- Every protocol server may reference the four, as it may `Surl.Cryptography`
  (ADR-0002 decision 3), so `Surl.Protocol.Ssh` can.
- The ADR also records what is *not* a primitive library, so no one rebuilds the BCL: SHA-2,
  HMAC, AES (the ECB transform AES-CTR is composed from), AES-GCM, ECDH and ECDSA over the NIST
  curves, RSA and `BigInteger` (finite-field Diffie-Hellman) come from the BCL; the SSH
  compositions `chacha20-poly1305@openssh.com`, AES-CTR and the Diffie-Hellman groups live in
  `Surl.Protocol.Ssh`. On the BCL's own `ChaCha20Poly1305`: cite Microsoft's documentation
  for where `ChaCha20Poly1305.IsSupported` is true, and state that SSH's construction (a
  separate length key, a 64-bit nonce from the sequence number) is not RFC 8439's AEAD, so it
  needs the raw ChaCha20 and Poly1305 anyway.
- Which SSH algorithms surl offers is BL-154's, not this ADR's; this ADR only decides where
  the hand-built pieces live. If BL-154 later finds curl offering an algorithm that needs
  another hand-built piece, it gets its own library by the same rule, in a new ADR.
- The Curl port's `Curl.Cryptography.UnitLibrary` (`Field25519.cs`, `X25519.cs`,
  `Edwards25519.cs`, `Scalar25519.cs`, `Ed25519.cs`, `ChaCha20.cs`, `Poly1305.cs`) may be
  copied as code; it is never evidence (ADR-0003).

## Acceptance criteria

- [x] A new ADR in `Documentation/Planning/Decisions/`, Status Accepted, "Decided by Claude
      under Stewart's delegation", holds the table in Context (names, contents,
      specifications, references) and the "not a primitive library" list.
- [x] `ADR-0002-mirror-the-curl-ports-project-map.md` carries an "Amended" line naming the new
      ADR, which states the four new rows of decision 3's table.
- [x] `Documentation/Planning/Decisions/README.md` indexes the new ADR.
- [x] `Documentation/Product/Product-Overview.md`: the "Layers" row "Hand-built primitives" and
      the "Project layout" table list the four libraries and their `.UnitTests` twins, written
      as intent until BL-149 creates them (e.g. "added by BL-149").

## Notes

- `ADR-0048-the-hand-built-ssh-primitive-libraries.md` decides the four libraries exactly
  as the Context table has them. ADR-0002 carries the Amended line and the four rows, the
  Decisions README indexes it, and `Product-Overview.md` lists them as "added by BL-149"
  (Layers row, Project layout, Rule 1's allowed list, Phase 2).
- Learned: Microsoft's cross-platform cryptography page (read 2026-09-29) lists the BCL's
  `ChaCha20Poly1305` as supported on macOS too (Windows 10 build 20142+, Linux with OpenSSL
  1.1.0+, macOS; iOS/tvOS/MacCatalyst from .NET 9; Android API 28; never the browser). It
  is still no use to SSH: OpenSSH's `chacha20-poly1305@openssh.com` uses a separate length
  key and a 64-bit sequence-number nonce, not RFC 8439's AEAD.
- Follow-up filed: BL-215 (root `CLAUDE.md`, the protocol-architect agent and
  `Surl.Protocol.Ssh.UnitLibrary/CLAUDE.md` still list only Content and Cryptography).
  `Surl.Cryptography.UnitLibrary/CLAUDE.md` and `ProtocolIsolationTests` are already
  BL-149's. FR-039, which this task cites, does not exist yet; BL-147 adds it.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ADR-0048 places Curve25519, Ed25519, ChaCha20 and Poly1305 in four hand-built libraries, added to ADR-0002's reference table
