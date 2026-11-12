---
id: BL-1010
title: Serve x448 in Surl.Tls13 through Surl.Cryptography.Curve448
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1007, BL-1009]
touches: [Cryptography/Surl.Tls13.UnitLibrary, Cryptography/Surl.Tls13.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: FR-055
created: 2026-11-06
completed:
---
# BL-1010 — Serve x448 in Surl.Tls13 through Surl.Cryptography.Curve448

## Goal

`Surl.Tls13` references `Surl.Cryptography.Curve448.UnitLibrary` and serves x448 (`0x001E`, RFC 7748 with RFC
8446 section 7.4.2's all-zero check) after the hybrids: a ClientHello offering only x448 gets a HelloRetryRequest
naming it and then completes, and the recorded upstream curl ClientHello for `--curves X448` completes without
a retry.

## Context

- Decision: the ADR BL-1005 records (decisions 1 to 3); the reference is allowed by its amendment of ADR-0002.
- Built on: BL-1007 (`X448`), BL-1009 (the order of the groups before x448).
- Split from BL-545 (timed out after 120 min, 2026-11-06). Reuse from the stash whose message is
  `darkfactory BL-545 20261106-033059` (commit `e132aaaa3985ff32f9f5f6e8838cbc123b5082ef`, called `S`), re-made
  by hand from `git diff "S^" "S" -- Cryptography/Surl.Tls13.UnitLibrary Cryptography/Surl.Tls13.UnitTests Foundation/Surl.Protocol.Abstractions.UnitTests`:
  the `Curve448` `ProjectReference` in `Surl.Tls13.UnitLibrary.csproj`; `X448` after the hybrids in
  `GroupPreference`; `AgreeX448` and `RequireNonZero` in `TlsKeyExchange.cs`; `WithX448Share` in
  `UnitTestTlsClient.cs`; the `0x001E` rows and `Receive_X448ShareOfZero_FailsWithIllegalParameterForTheAllZeroSecret`
  from the stash's `TlsServerHandshakeTests.Groups.cs`; and in `ProtocolIsolationTests.cs` `Curve448` in the
  `[Tls13]` row and `[DataRow(Tls13, Curve448)]`.
- Add the x448 bullet of the stash's `Cryptography/Surl.Tls13.UnitLibrary/CLAUDE.md` diff, "an x448 low-order
  point" in its `illegal_parameter` row, and `Surl.Cryptography.Curve448.UnitLibrary` in its references paragraph.
- Stand-in: not applicable; the TLS handshake gains a group and no store, event or fault.

## Acceptance criteria

- [ ] `Receive_ClientHelloOfferingOnlyOneRemainingGroupWithNoShare_RetriesForItAndCompletesOnTheSecondClientHello`
      passes for `0x001E`.
- [ ] `Receive_RecordedUpstreamCurlClientHelloForOneCurve_AnswersItsShareAndCompletesWithoutARetry` passes for both
      `X448` fixtures (Windows and Linux).
- [ ] `Receive_RemainingGroupShareOfTheWrongLength_FailsWithIllegalParameter` passes for `(0x001E, 55)`, and
      `Receive_X448ShareOfZero_FailsWithIllegalParameterForTheAllZeroSecret` passes.
- [ ] `ProtocolIsolationTests` passes with `Surl.Tls13.UnitLibrary` referencing `Curve25519`, `Curve448` and `MlKem`.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Tls13.UnitLibrary` keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-545 (timed out after 120 min); reuses its x448 key exchange from stash `darkfactory BL-545 20261106-033059`.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. returned by the shift starting 20261111-212123: it was in Doing but no stopped lane held it; look for its work in 'git stash list' and in factory/BL-1010-* branches before redoing it
- 2026-11-11: Backlog -> Doing.
