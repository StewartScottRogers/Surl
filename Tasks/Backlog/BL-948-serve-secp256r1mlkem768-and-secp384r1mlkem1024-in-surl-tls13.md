---
id: BL-948
title: Serve SecP256r1MLKEM768 and SecP384r1MLKEM1024 in Surl.Tls13
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-944, BL-947]
touches: [Cryptography/Surl.Tls13.UnitLibrary, Cryptography/Surl.Tls13.UnitTests]
requirement: FR-055
created: 2026-11-06
completed:
---
# BL-948 — Serve SecP256r1MLKEM768 and SecP384r1MLKEM1024 in Surl.Tls13

## Goal

`Surl.Tls13` serves the hybrids SecP256r1MLKEM768 (`0x11EB`) and SecP384r1MLKEM1024 (`0x11ED`,
draft-ietf-tls-ecdhe-mlkem), placed right after ADR-0082 decision 3's five groups: a ClientHello offering only
one gets a HelloRetryRequest naming it and then completes, and the recorded upstream curl ClientHello for
`--curves` with either completes without a retry.

## Context

- Decision: the ADR BL-943 records (decision 2): the client's share is the uncompressed NIST point then the
  ML-KEM encapsulation key (1249 and 1665 bytes), the server's its point then the ciphertext, and the secret
  the ECDH secret then ML-KEM's; randomness ML-KEM's m first. The NIST curves are the BCL's `ECDiffieHellman`.
- Built on: BL-944 (`MlKem1024`) and BL-947 (fixtures, `TlsServerHandshakeTests.Groups.cs`, `partial` test class).
- Split from BL-545 (timed out after 120 min, 2026-11-06). Reuse from the stash whose message is
  `darkfactory BL-545 20261106-033059` (commit `e132aaaa3985ff32f9f5f6e8838cbc123b5082ef`, called `S`), re-made
  by hand from `git diff "S^" "S" -- Cryptography/Surl.Tls13.UnitLibrary Cryptography/Surl.Tls13.UnitTests`,
  since BL-947 has changed the same files: in `TlsNamedGroup.cs` the `SecP256r1MlKem768` and
  `SecP384r1MlKem1024` members (`0x11ED`, not `0x11EE`); in `TlsServerNegotiation.GroupPreference` both after
  `Secp521r1`; in `TlsKeyExchange.cs` the `TlsMlKem` record, `MlKem768Kem`, `MlKem1024Kem`, `Encapsulate`,
  `AgreeClassicalFirstHybrid`, `EncapsulateTo` and `Combine`, dispatching only `0x11EB` and `0x11ED`; in
  `UnitTestTlsClient.cs` `WithClassicalFirstHybridShare` for `0x11EB` and `0x11ED`, `MlKem768Key` and
  `MlKem1024Key`. Add back the `0x11EB` and `0x11ED` rows of the stash's `TlsServerHandshakeTests.Groups.cs`.
- Add the hybrids bullet of the stash's `Cryptography/Surl.Tls13.UnitLibrary/CLAUDE.md` diff for these two
  groups, and its "ML-KEM encapsulation key" wording in the `illegal_parameter` row.
- Stand-in: not applicable; the TLS handshake gains groups and no store, event or fault.

## Acceptance criteria

- [ ] `Receive_ClientHelloOfferingOnlyOneRemainingGroupWithNoShare_RetriesForItAndCompletesOnTheSecondClientHello`
      passes for `0x11EB` and `0x11ED`.
- [ ] `Receive_RecordedUpstreamCurlClientHelloForOneCurve_AnswersItsShareAndCompletesWithoutARetry` passes for the
      four `SecP256r1MLKEM768` and `SecP384r1MLKEM1024` fixtures (Windows and Linux).
- [ ] `Receive_RemainingGroupShareOfTheWrongLength_FailsWithIllegalParameter` passes for `(0x11EB, 1248)`,
      `(0x11EB, 1184)` and `(0x11ED, 1666)`.
- [ ] Every existing `Surl.Tls13.UnitTests` test passes.
- [ ] `dotnet build Cryptography/Surl.Tls13.UnitTests -warnaserror` is clean, the fast tests are green, and
      `Surl.Tls13.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-545 (timed out after 120 min); reuses its hybrid key exchange from stash `darkfactory BL-545 20261106-033059`.
