---
id: BL-412
title: Create Surl.Dtls with the DTLS 1.2 record layer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-403, BL-404]
touches: [Cryptography/Surl.Dtls.UnitLibrary, Cryptography/Surl.Dtls.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-412 — Create Surl.Dtls with the DTLS 1.2 record layer

## Goal

`Surl.Dtls` reads and writes DTLS 1.2 records (RFC 6347 section 4.1). It handles epochs, 48-bit sequence numbers,
the anti-replay window, and AEAD protection under the AES-GCM and AES-CCM_8 suites BL-403's ADR chose.
Records travel over an `IDatagramFlow`.

## Context

- Decisions: BL-401's ADR (the library's name, folder and ADR-0002 row; planning name `Surl.Dtls` in
  `Cryptography/`) and BL-403's ADR (the suites). If the folder or name differ, use the ADR's and correct
  `touches`.
- RFC 6347 section 4.1 (record layer, epoch, sequence number), section 4.1.2.6 (replay detection: a sliding
  window), and section 4.1.2.7 (records with bad MACs or the wrong epoch are discarded silently, not alerted).
  RFC 5288 (AES-GCM: the 4-byte implicit and 8-byte explicit nonce) and RFC 6655 / RFC 7251 (AES-CCM_8).
  The additional data is RFC 5246 section 6.2.3.3's, with `seq_num` replaced by epoch and sequence number
  (RFC 6347 section 4.1.2.1).
- BCL: `AesGcm` and `AesCcm` (an 8-byte tag). No socket: `IDatagramFlow` from `Surl.Protocol.Abstractions`.
- New projects in the ADR's folder, listed in `Surl.slnx`. The row joins `ProtocolIsolationTests`.

## Acceptance criteria

- [x] `Surl.Dtls.UnitLibrary` and `Surl.Dtls.UnitTests` exist and are in `Surl.slnx`.
- [x] Named tests cover:
      - the 13-byte record header read and written;
      - a record sealed and opened under each chosen suite, with the nonce and additional data built as the
        RFCs say and checked against a hand-computed vector in the test;
      - a replayed record dropped;
      - a record older than the window dropped;
      - a record with a corrupted tag dropped silently;
      - a record of the wrong epoch dropped;
      - sequence-number exhaustion refused.
- [x] `ProtocolIsolationTests` passes with the library's row, `dotnet build -warnaserror` is clean, the fast tests
      are green, and the library keeps 100% line and branch coverage.

## Notes

- Built to ADR-0084 decision 3 (name, `Cryptography/` folder, row `[Abstractions, Curve25519]` as
  ADR-0087 amends it) and ADR-0087 decisions 2 and 8. No new ADR: every choice below sits inside
  decision 8.
- Types: `DtlsRecordHeader`, `DtlsRecordProtection` (`DtlsAeadAlgorithm` AesGcm / AesCcm / AesCcm8),
  `DtlsReplayWindow`, `DtlsRecordWriter`, `DtlsRecordReader` (returns `DtlsDatagramRecords` with a
  `DtlsRecordDropReason` per drop, for the verbose-log note), `DtlsRecordLayer` over `IDatagramFlow`.
- Choice: only the current read epoch is accepted. Epoch 1 records arriving before the peer's
  `ChangeCipherSpec` are dropped `WrongEpoch` rather than buffered (RFC 6347 section 4.1 allows
  either); the handshake's retransmission recovers them. BL-413/414 may add buffering if needed.
- Choice: the window is marked only after the tag verifies (section 4.1.2.6), so a forged record
  never burns a sequence number - pinned by `ReadDatagram_CorruptedRecord_LeavesItsSequenceNumberOpen`.
- Choice: sequence number 2^48 - 1 is usable; the next seal throws
  `DtlsSequenceNumberExhaustedException`. BL-415's close should send `close_notify` before that
  (watch `NextWriteSequenceNumber`).
- Choice: the record's version is exposed, not judged; the handshake (BL-413/414) decides which
  versions it answers (a first `ClientHello` may carry DTLS 1.0's 0xFEFF).
- Vectors: each suite's sealed fragment is compared with the BCL AEAD run on a nonce and additional
  data written out as hex literals from the RFC layout in the test.
- AES-CCM tests are excluded on macOS (`OSCondition`) because `AesCcm` support there is not assured;
  the handshake must not offer CCM suites when `AesCcm.IsSupported` is false (noted in the project's
  CLAUDE.md for BL-414/415).
- Results: 51 tests in `Surl.Dtls.UnitTests`, 100% line and branch coverage of the library,
  `dotnet build -warnaserror` clean, every fast test project green.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Surl.Dtls reads and writes DTLS 1.2 records: epochs, 48-bit sequence numbers, 64-record replay window, AES-GCM/CCM/CCM_8 protection over IDatagramFlow
