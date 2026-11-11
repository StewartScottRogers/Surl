---
id: BL-1006
title: Add ML-KEM-1024 beside ML-KEM-768 in Surl.Cryptography.MlKem
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1005]
touches: [Cryptography/Surl.Cryptography.MlKem.UnitLibrary, Cryptography/Surl.Cryptography.MlKem.UnitTests]
requirement: FR-055
created: 2026-11-06
completed: 2026-11-11
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

- [x] `MlKem1024Tests` exists and passes, including
      `GenerateKey_AcvpKeyGenCases_WriteTheVectorsKeys`, `TryEncapsulate_AcvpEncapsulationCases_WriteTheVectorsCiphertextAndSecret`,
      `TryDecapsulate_AcvpDecapsulationCases_WriteTheVectorsSecret` and the two `...KeyChecks_RejectOnlyTheKeysTheVectorsFail` tests.
- [x] Every existing `MlKem768Tests` test passes unchanged in what it asserts.
- [x] `dotnet build Cryptography/Surl.Cryptography.MlKem.UnitTests -warnaserror` is clean and the fast tests are green.
- [x] `Surl.Cryptography.MlKem.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Restored from stash commit `e132aaaa` (BL-545): the four untracked files were written with `git show "S^3:<path>"` (a `git checkout` into the index was refused by the sandbox), and the tracked diff applied cleanly with `git apply` - none of the files had changed since `S^`.
- The restored `CLAUDE.md` held no `ADR-0142`; it now names ADR-0147 decision 3, the ADR BL-1005 wrote, as why `MlKem1024` exists.
- `MlKem768Tests` changes only which fixture it reads (`UnitTestAcvpVectors.Cases("ML-KEM-768", ...)`); every assertion is as before.
- Toolchain: SDK 10.0.401, which `global.json` pins, is no longer installed (only 10.0.100-rc.1 and 11.0.100). Verified by moving this worktree's `global.json` aside for the run only (restored, unchanged in git) and using SDK 11.0.100 for the `net10.0` target: `dotnet build -warnaserror` of the test project clean, whole-solution `dotnet build` 0 warnings 0 errors, fast tests 79 assemblies passed 0 failed, `MlKem` tests 42 passed. `Measure-CodeQuality.ps1`: `Surl.Cryptography.MlKem.UnitLibrary` 100% line, 100% branch, 54 members, 0 failing, worst CRAP 8. Filed BL-1035 (Stewart) to reinstall the pinned SDK.

## Log

- 2026-11-06: Created.
- 2026-11-06: Split from BL-545 (timed out after 120 min); reuses its ML-KEM-1024 work from stash `darkfactory BL-545 20261106-033059`.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. MlKem1024 (FIPS 203) passes every NIST ACVP ML-KEM-1024 vector beside MlKem768, both over one shared KPke parameter set; 100% line and branch
