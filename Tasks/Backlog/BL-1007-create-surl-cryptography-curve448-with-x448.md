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
completed:
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

- [ ] `X448Tests` exists and passes, including `ScalarMultiply_Rfc7748Section52FirstVector_ReturnsTheRfcOutput`,
      `ScalarMultiply_Rfc7748Section52IteratedAThousandTimes_ReturnsTheRfcOutput`,
      `ScalarMultiply_Rfc7748Section62KeyPair_ReturnsTheSharedSecret` and
      `ScalarMultiply_LowOrderPointZero_ReturnsTheAllZeroResultAsComputed`.
- [ ] `Surl.slnx` lists both projects directly after `Surl.Cryptography.Curve25519.UnitTests`.
- [ ] `ProtocolIsolationTests` passes with `Surl.Cryptography.Curve448.UnitLibrary` in its table, referencing nothing.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.
- [ ] `Surl.Cryptography.Curve448.UnitLibrary` has 100% line and branch coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-545 (timed out after 120 min); reuses its Curve448 projects from stash `darkfactory BL-545 20261106-033059`.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 3 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-1007-lane-3-20261111-060243; start with git cherry-pick --no-commit factory/BL-1007-lane-3-20261111-060243 and fix it.
