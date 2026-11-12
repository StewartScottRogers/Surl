---
id: BL-1030
title: Serve curveSM2 and curveSM2MLKEM768 in Surl.Tls13 through Surl.Cryptography.Sm2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1029, BL-1010]
touches: [Cryptography/Surl.Tls13.UnitLibrary, Cryptography/Surl.Tls13.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: FR-055
created: 2026-11-06
completed:
---
# BL-1030 — Serve curveSM2 and curveSM2MLKEM768 in Surl.Tls13 through Surl.Cryptography.Sm2

## Goal

`Surl.Tls13` references `Surl.Cryptography.Sm2.UnitLibrary` and serves curveSM2MLKEM768 (`0x11EE`, after
SecP384r1MLKEM1024) and curveSM2 (`0x0029`, RFC 8998, after x448): a ClientHello offering only one gets a
HelloRetryRequest naming it and then completes, and the recorded upstream curl ClientHello for `--curves` with
either completes without a retry. With this, every group BL-545 named is served.

## Context

- Decision: the ADR BL-1005 records (decision 2): curveSM2 is an uncompressed point and the secret its product's
  x-coordinate, with a 32-byte private key from the injected `RandomNumberGenerator`; curveSM2MLKEM768
  (draft-yang-tls-hybrid-sm2-mlkem) is laid out like SecP256r1MLKEM768 with curveSM2 in the NIST curve's place.
- Built on: BL-1029 (`CurveSm2`), BL-1009 (`AgreeClassicalFirstHybrid`), BL-1010.
- Split from BL-545 (timed out after 120 min, 2026-11-06). Reuse from the stash whose message is
  `darkfactory BL-545 20261106-033059` (commit `e132aaaa3985ff32f9f5f6e8838cbc123b5082ef`, called `S`), re-made
  by hand from `git diff "S^" "S" -- Cryptography/Surl.Tls13.UnitLibrary Cryptography/Surl.Tls13.UnitTests Foundation/Surl.Protocol.Abstractions.UnitTests`:
  the `Sm2` `ProjectReference`; the `CurveSm2` and `CurveSm2MlKem768` members of `TlsNamedGroup`;
  `CurveSm2MlKem768` after `SecP384r1MlKem1024` and `CurveSm2` after `X448` in `GroupPreference`; the
  `0x11EE` dispatch and `AgreeCurveSm2` in `TlsKeyExchange.cs`; `WithCurveSm2Share` and the curveSM2 arm of
  `WithClassicalFirstHybridShare` in `UnitTestTlsClient.cs`; the `0x11EE` and `0x0029` rows and the
  curveSM2 point-format tests of the stash's `TlsServerHandshakeTests.Groups.cs`; and in
  `ProtocolIsolationTests.cs` `Sm2` in the `[Tls13]` row and `[DataRow(Tls13, Sm2)]`.
- Finish `Cryptography/Surl.Tls13.UnitLibrary/CLAUDE.md` as the stash's diff has it: the curveSM2 bullet,
  curveSM2MLKEM768 in the hybrids bullet, "a NIST or curveSM2 point off its curve" in the `illegal_parameter`
  row, `Surl.Cryptography.Sm2.UnitLibrary` in the references paragraph, all citing BL-1005's ADR number.
- Stand-in: not applicable; the TLS handshake gains groups and no store, event or fault.

## Acceptance criteria

- [ ] `Receive_ClientHelloOfferingOnlyOneRemainingGroupWithNoShare_RetriesForItAndCompletesOnTheSecondClientHello`
      passes for `0x11EE` and `0x0029`.
- [ ] `Receive_RecordedUpstreamCurlClientHelloForOneCurve_AnswersItsShareAndCompletesWithoutARetry` passes for the
      four `curveSM2` and `curveSM2MLKEM768` fixtures (Windows and Linux).
- [ ] `Receive_RemainingGroupShareOfTheWrongLength_FailsWithIllegalParameter` passes for `(0x11EE, 1250)` and
      `(0x0029, 64)`, and a curveSM2 point off the curve fails with `illegal_parameter`.
- [ ] `ProtocolIsolationTests` passes with `Surl.Tls13.UnitLibrary` referencing `Curve25519`, `Curve448`, `MlKem` and `Sm2`.
- [ ] `TlsServerNegotiation.GroupPreference` is the twelve groups in the ADR's order.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Tls13.UnitLibrary` keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-545 (timed out after 120 min); reuses its curveSM2 key exchange from stash `darkfactory BL-545 20261106-033059`.
- 2026-11-12: Backlog -> Doing.
