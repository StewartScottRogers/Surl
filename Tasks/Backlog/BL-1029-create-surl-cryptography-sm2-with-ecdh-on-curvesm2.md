---
id: BL-1029
title: Create Surl.Cryptography.Sm2 with ECDH on curveSM2
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1005, BL-1035]
touches: [Cryptography/Surl.Cryptography.Sm2.UnitLibrary, Cryptography/Surl.Cryptography.Sm2.UnitTests, Surl.slnx, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: FR-055
created: 2026-11-06
completed:
---
# BL-1029 — Create Surl.Cryptography.Sm2 with ECDH on curveSM2

## Goal

A new `Cryptography/Surl.Cryptography.Sm2.UnitLibrary`, referencing nothing, computes curveSM2 public keys and
the elliptic-curve Diffie-Hellman shared secret (the product's x-coordinate, RFC 8998), with its `.UnitTests`
twin, registered in `Surl.slnx` and in `ProtocolIsolationTests`' table, so `Surl.Tls13` can serve curveSM2.

## Context

- Decision: the ADR BL-1005 records (decision 3's table). The BCL has no SM2 curve, and an explicit curve is
  not available on macOS, so it is hand-built (GB/T 32918.5's recommended curve).
- Split from BL-545 (timed out after 120 min, 2026-11-06). Its lane finished this library: 45 tests pass,
  100% line and branch. Reuse it from the stash whose message is `darkfactory BL-545 20261106-033059`
  (commit `e132aaaa3985ff32f9f5f6e8838cbc123b5082ef`, called `S`; find its index with `git stash list`):
  - untracked, with `git checkout "S^3" -- Cryptography/Surl.Cryptography.Sm2.UnitLibrary Cryptography/Surl.Cryptography.Sm2.UnitTests`:
    `CLAUDE.md`, `CurveSm2.cs`, `CurveSm2Parameters.cs`, `Sm2AffinePoint.cs`, `Sm2JacobianPoint.cs`,
    `Sm2PointArithmetic.cs` (about 360 lines together), the csproj, and `CurveSm2Tests.cs`,
    `CurveSm2ParametersTests.cs`, `Sm2PointArithmeticTests.cs` and the tests' csproj. In `CLAUDE.md` replace
    any `ADR-0142` with the number BL-1005 gave its ADR.
  - tracked, re-made by hand (shared files that keep moving): the two `Sm2` lines of
    `git diff "S^" "S" -- Surl.slnx`, and of
    `git diff "S^" "S" -- Foundation/Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs` only the
    `Sm2` constant, its `[Sm2] = []` row, its `[DataRow(Sm2)]` and the `[DataRow(Sm2, Cryptography)]`
    forbidden-reference row. Leave the `Tls13` row to BL-1030.
- Expected values come from GM/T 0003.5's published key pair and key-exchange example, never from the Curl port.
- Stand-in: not applicable; a primitive with no store, event or fault.

## Acceptance criteria

- [ ] `CurveSm2Tests` exists and passes, including `ComputePublicKey_GmT00035PublishedKeyPair_ReturnsThePublishedPoint`,
      `TryComputeSharedSecret_GmT00035KeyExchange_ReturnsTheSharedPointsX`,
      `TryComputeSharedSecret_InvalidPeerPoint_ReturnsFalseAndWritesNothing` and
      `TryComputeSharedSecret_ProductAtInfinity_ReturnsFalseAndWritesNothing`.
- [ ] `Surl.slnx` lists both projects directly after `Surl.Cryptography.Ripemd160.UnitTests`.
- [ ] `ProtocolIsolationTests` passes with `Surl.Cryptography.Sm2.UnitLibrary` in its table, referencing nothing.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.
- [ ] `Surl.Cryptography.Sm2.UnitLibrary` has 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 4): not started - `dotnet` cannot resolve the SDK `global.json` pins (10.0.401; installed are
  10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100, and `rollForward: latestFeature` does not cross a major version), so
  no acceptance gate (build, tests, coverage) can be checked. The task waits on BL-1035. The finished Sm2 code
  is still in stash `darkfactory BL-545 20261106-033059` (commit `e132aaaa`), as Context says; nothing was
  restored from it, so nothing is left to stash.

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-545 (timed out after 120 min); reuses its Sm2 projects from stash `darkfactory BL-545 20261106-033059`.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing can be built or tested
