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
completed: 2026-11-05
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

- [x] Named tests check the PRF against published TLS 1.2 PRF test vectors (SHA-256) and the
      master-secret and key-block derivation against values computed by hand in the test from
      RFC 5246's formulas.
- [x] Named tests round-trip records for each cipher suite the ADR serves, fragment a payload over
      2^14 bytes, reject a record over 2^14 + 2048 bytes, reject a bad MAC and bad padding with the
      same alert (`bad_record_mac`), and reject a replayed sequence number.
- [x] `dotnet build Cryptography/Surl.TlsSrp.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and the library has 100% line and branch coverage, complexity of at most 10 per method
      and CRAP of at most 30.

## Notes

- Built per ADR-0122 decisions 1 to 3: `TlsPseudorandomFunction` (SHA-256 PRF, master secret,
  extended master secret, key block, both `verify_data`), `TlsSrpCipherSuite`/`TlsSrpCipherSuites`/
  `TlsBulkCipher` (all nine suites; 3DES, AES-128, AES-256 CBC with HMAC-SHA1), `TlsKeyBlock`,
  `TlsCbcRecordProtection` (MAC-then-encrypt and RFC 7366 encrypt-then-MAC, both directions'
  sequence numbers in the MAC), `TlsCbcPadding`, `TlsRecordReader` and `TlsRecordWriter`.
- Nothing shared with `Surl.Dtls` or `Surl.Tls13`, as ADR-0122 decision 1 says: the PRF is a
  separate copy, tested against the same Birr-Pixton SHA-256 vector.
- Choice: the library does not reference `Surl.Cryptography.Srp.UnitLibrary` yet, because nothing
  in the record layer uses it; `ProtocolIsolationTests` already allows `[TlsSrp] = [Srp]`, so
  BL-566 adds the reference when the handshake needs it.
- Choice: the master secret, key block and `verify_data` tests compare against `UnitTestPrf`, RFC
  5246 section 5's `A(i)`/`P_SHA256` written out by hand in the test project, itself checked
  against the published vector.
- Choice: a replayed record is refused because the reader's sequence number has moved on, so its
  MAC fails with `bad_record_mac` (TLS over TCP has no explicit sequence number to compare). A
  64-bit sequence number never wraps in practice, so no wrap check was added.
- Choice: the reader returns `null` when the stream ends between records and throws
  `EndOfStreamException` inside one; an unknown content type is `unexpected_message`. The
  writer writes an empty payload as one empty record.
- Choice: a fragment too short or of the wrong length for its cipher is `bad_record_mac`, the
  same alert as a bad MAC or bad padding, so the length gives nothing away either.
- Measured: `Surl.TlsSrp.UnitLibrary` 100% line, 100% branch, 39 members, worst CRAP 10
  (`Measure-CodeQuality.ps1 -SkipTestRun` over this project's Cobertura run). 84 tests.
- `dotnet format --verify-no-changes` reports only ENDOFLINE on this LF worktree (1262
  repo-wide, there before this task); nothing else in the touched files.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.TlsSrp reads and writes TLS 1.2 CBC records (MAC-then-encrypt and encrypt-then-MAC) for all nine RFC 5054 suites, with the SHA-256 PRF, master secrets and key block
