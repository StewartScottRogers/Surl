---
id: BL-1006
title: Add ML-KEM-1024 beside ML-KEM-768 in Surl.Cryptography.MlKem
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1005, BL-1035]
touches: [Cryptography/Surl.Cryptography.MlKem.UnitLibrary, Cryptography/Surl.Cryptography.MlKem.UnitTests]
requirement: FR-055
created: 2026-11-06
completed:
---
# BL-1006 — Add ML-KEM-1024 beside ML-KEM-768 in Surl.Cryptography.MlKem

## Goal

`Surl.Cryptography.MlKem` offers `MlKem1024` (FIPS 203) with the same public shape as `MlKem768`, both
sharing K-PKE through a parameter set, so `Surl.Tls13` can serve SecP384r1MLKEM1024.

## Context

- Decision: the ADR BL-1005 records (decision 3: ML-KEM-1024 beside ML-KEM-768).
- Split from BL-545 (timed out after 120 min, 2026-11-06). Its lane finished this piece: 100% line and branch,
  worst CRAP 8, all ACVP vectors passing. Reuse it from the stash whose message is
  `darkfactory BL-545 20261106-033059` (commit `e132aaaa3985ff32f9f5f6e8838cbc123b5082ef`, called `S` here;
  find its index with `git stash list`):
  - untracked, with `git checkout "S^3" -- <path>`:
    `Cryptography/Surl.Cryptography.MlKem.UnitLibrary/MlKem1024.cs`,
    `Cryptography/Surl.Cryptography.MlKem.UnitLibrary/MlKemKeyEncapsulation.cs`,
    `Cryptography/Surl.Cryptography.MlKem.UnitTests/MlKem1024Tests.cs`,
    `Cryptography/Surl.Cryptography.MlKem.UnitTests/Fixtures/Acvp/ML-KEM-1024.json`;
  - tracked, with `git diff "S^" "S" -- Cryptography/Surl.Cryptography.MlKem.UnitLibrary Cryptography/Surl.Cryptography.MlKem.UnitTests | git apply`
    (none of these files changed on the shared branch since): `KPke.cs`, `MlKem768.cs`,
    `MlKemPolynomial.cs`, `CLAUDE.md`, `MlKem768Tests.cs`, `UnitTestAcvpVectors.cs`.
  - In the library's `CLAUDE.md`, replace any `ADR-0142` with the number BL-1005 gave its ADR.
- Every expected value comes from NIST's ACVP ML-KEM-1024 vectors (the fixture above), never from the Curl port.
- Stand-in: not applicable; a key-encapsulation primitive with no store, event or fault.

## Acceptance criteria

- [ ] `MlKem1024Tests` exists and passes, including
      `GenerateKey_AcvpKeyGenCases_WriteTheVectorsKeys`, `TryEncapsulate_AcvpEncapsulationCases_WriteTheVectorsCiphertextAndSecret`,
      `TryDecapsulate_AcvpDecapsulationCases_WriteTheVectorsSecret` and the two `...KeyChecks_RejectOnlyTheKeysTheVectorsFail` tests.
- [ ] Every existing `MlKem768Tests` test passes unchanged in what it asserts.
- [ ] `dotnet build Cryptography/Surl.Cryptography.MlKem.UnitTests -warnaserror` is clean and the fast tests are green.
- [ ] `Surl.Cryptography.MlKem.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 4): The code is finished on branch `factory/BL-1006-lane-3-20261111-060243` (commit `04a3712c`). Cherry-picked onto lane 4; with `global.json` moved aside for the run only (SDK 11.0.100 building `net10.0`), `dotnet build Cryptography/Surl.Cryptography.MlKem.UnitTests -warnaserror` is clean and the 42 MlKem tests pass. With `global.json` in place no build can run: SDK 10.0.401 is not installed, which is why lane 3's integration build failed. Waits on BL-1035 (Stewart reinstalls the SDK); then `git cherry-pick 04a3712c`, build, run the fast tests and finish.

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-545 (timed out after 120 min); reuses its ML-KEM-1024 work from stash `darkfactory BL-545 20261106-033059`.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 3 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-1006-lane-3-20261111-060243; start with git cherry-pick --no-commit factory/BL-1006-lane-3-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so no build runs; code is done on factory/BL-1006-lane-3-20261111-060243 (04a3712c).
