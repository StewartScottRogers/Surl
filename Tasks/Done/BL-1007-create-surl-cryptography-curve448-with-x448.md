---
id: BL-1007
title: Create Surl.Cryptography.Curve448 with X448
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1005]
touches: [Cryptography/Surl.Cryptography.Curve448.UnitLibrary, Cryptography/Surl.Cryptography.Curve448.UnitTests, Surl.slnx, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: FR-055
created: 2026-11-06
completed: 2026-11-11
---
# BL-1007 — Create Surl.Cryptography.Curve448 with X448

## Goal

A new `Cryptography/Surl.Cryptography.Curve448.UnitLibrary`, referencing nothing, computes X448 (RFC 7748)
public keys and shared secrets, with its `.UnitTests` twin, registered in `Surl.slnx` and in
`ProtocolIsolationTests`' table, so `Surl.Tls13` can serve x448.

## Context

- Decision: the ADR BL-1005 records (decision 3's table; ADR-0002 amended by it). The BCL has no X448 on any
  platform, so it is hand-built.
- Split from BL-545 (timed out after 120 min, 2026-11-06). Its lane finished this library: every RFC 7748
  vector passes, 100% line and branch. Reuse it from the stash whose message is
  `darkfactory BL-545 20261106-033059` (commit `e132aaaa3985ff32f9f5f6e8838cbc123b5082ef`, called `S`; find its
  index with `git stash list`):
  - untracked, with `git checkout "S^3" -- Cryptography/Surl.Cryptography.Curve448.UnitLibrary Cryptography/Surl.Cryptography.Curve448.UnitTests`:
    `CLAUDE.md`, `Field448.cs` (302 lines), `X448.cs` (168), the csproj, and `Field448Tests.cs`, `X448Tests.cs`
    and the tests' csproj. In `CLAUDE.md` replace any `ADR-0142` with the number BL-1005 gave its ADR.
  - tracked, re-made by hand (both files are shared and keep moving): the two `Curve448` lines of
    `git diff "S^" "S" -- Surl.slnx`, and of
    `git diff "S^" "S" -- Foundation/Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs` only the
    `Curve448` constant, its `[Curve448] = []` row, its `[DataRow(Curve448)]` and the
    `[DataRow(Curve448, Curve25519)]` forbidden-reference row. Leave the `Sm2` lines and the `Tls13` row to
    their own tasks.
- Every expected value comes from RFC 7748 sections 5.2 and 6.2, never from the Curl port.
- Stand-in: not applicable; a primitive with no store, event or fault.

## Acceptance criteria

- [x] `X448Tests` exists and passes, including `ScalarMultiply_Rfc7748Section52FirstVector_ReturnsTheRfcOutput`,
      `ScalarMultiply_Rfc7748Section52IteratedAThousandTimes_ReturnsTheRfcOutput`,
      `ScalarMultiply_Rfc7748Section62KeyPair_ReturnsTheSharedSecret` and
      `ScalarMultiply_LowOrderPointZero_ReturnsTheAllZeroResultAsComputed`.
- [x] `Surl.slnx` lists both projects directly after `Surl.Cryptography.Curve25519.UnitTests`.
- [x] `ProtocolIsolationTests` passes with `Surl.Cryptography.Curve448.UnitLibrary` in its table, referencing nothing.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.
- [x] `Surl.Cryptography.Curve448.UnitLibrary` has 100% line and branch coverage.

## Notes

- Restored the seven Curve448 files from the stash's untracked tree (`e132aaaa^3`) with `git show`, file by
  file; `CLAUDE.md`'s `ADR-0142` is now `ADR-0147`, the number BL-1005 gave its ADR. Re-made by hand only the
  two `Surl.slnx` lines and the four `Curve448` lines of `ProtocolIsolationTests`; the `Sm2` lines and the
  `Tls13` row stay with their own tasks.
- Environment, 2026-11-11: the pinned SDK 10.0.401 is not installed on this machine (only 10.0.100-rc.1 and
  11.0.x are), so `dotnet` refuses to run under `global.json`. The build (`-warnaserror`, 0 warnings), the
  fast tests (80 test assemblies, all passing, Curve448 20/20) and the coverage (`Measure-CodeQuality.ps1`:
  100% line, 100% branch, 0 failing members) were run with `global.json`'s `rollForward` set to
  `latestMajor` for the run only, so the net10.0 projects were built by SDK 11.0.100. That change was
  reverted and is not committed. CI builds with the pinned SDK.

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-545 (timed out after 120 min); reuses its Curve448 projects from stash `darkfactory BL-545 20261106-033059`.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Surl.Cryptography.Curve448 computes X448 public keys and shared secrets to every RFC 7748 vector, listed in Surl.slnx and ProtocolIsolationTests with no references
