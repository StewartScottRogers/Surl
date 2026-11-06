---
id: BL-565
title: Create Surl.TlsSrp with the TLS 1.2 record layer and key derivation
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-563]
touches: [Surl.slnx, Cryptography/Surl.TlsSrp.UnitLibrary, Cryptography/Surl.TlsSrp.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-565 — Create Surl.TlsSrp with the TLS 1.2 record layer and key derivation

## Goal

`Surl.TlsSrp.UnitLibrary` exists with its `.UnitTests` twin and reads and writes TLS 1.2 records:
plaintext records before `ChangeCipherSpec`, and the CBC cipher suites BL-563's ADR serves after
it (MAC-then-encrypt with HMAC-SHA1 and an explicit IV, or encrypt-then-MAC if the ADR negotiates
it), with the TLS 1.2 PRF deriving the master secret and key block.

## Context

- Decision: BL-563's ADR, decisions 1 to 3. The library name and folder are the ADR's.
- Specification: RFC 5246 section 5 (PRF with SHA-256), 6.2 (record layer, fragmentation, the
  64-bit sequence number in the MAC), 6.2.3.2 (CBC block cipher, padding), 6.3 (key calculation),
  8.1 (master secret); RFC 7627 (extended master secret) and RFC 7366 (encrypt-then-MAC) if the
  ADR serves them. Padding errors and MAC errors are answered alike (RFC 5246 section 6.2.3.2's
  note on timing).
- BCL only: `Aes` and `TripleDES` in CBC mode without padding, `HMACSHA1`, `HMACSHA256`. If the
  ADR shares PRF or record code with `Surl.Dtls` or `Surl.Tls13`, follow it and say so in Notes.
- New project steps: add both projects to `Surl.slnx` under `/Cryptography/` in ordinal order, give
  the library its `CLAUDE.md`, and update `Surl.Protocol.Abstractions.UnitTests`' project-reference
  rules if they list the hand-built libraries.
- Upstream test cases that wait on this (tag `curl-8_21_0`), `httptls+srp`: 320, 321, 322, 324.

## Acceptance criteria

- [ ] Named tests check the PRF against published TLS 1.2 PRF test vectors (SHA-256) and the
      master-secret and key-block derivation against values computed by hand in the test from
      RFC 5246's formulas.
- [ ] Named tests round-trip records for each cipher suite the ADR serves, fragment a payload over
      2^14 bytes, reject a record over 2^14 + 2048 bytes, reject a bad MAC and bad padding with the
      same alert (`bad_record_mac`), and reject a replayed sequence number.
- [ ] `dotnet build Cryptography/Surl.TlsSrp.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and the library has 100% line and branch coverage, complexity of at most 10 per method
      and CRAP of at most 30.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
