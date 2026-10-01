---
id: BL-353
title: Import a fixed DSA test key in Surl.Protocol.Ssh.UnitTests so the macOS fast tests pass
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Surl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-353 — Import a fixed DSA test key in Surl.Protocol.Ssh.UnitTests so the macOS fast tests pass

## Goal

`Surl.Protocol.Ssh.UnitTests` passes on macOS: its 1024-bit DSA test key is imported from
fixed parameters instead of generated, because macOS's BCL cannot generate DSA keys.

## Context

- CI run 36840335608 (2026-10-01, commit 33959f8b), job "Build and test (macos-latest)",
  step "Fast tests": 47 of 1040 tests in `Surl.Protocol.Ssh.UnitTests` fail with
  `System.PlatformNotSupportedException: DSA keys can be imported, but new key generation is
  not supported on this platform.` from `SshTestKeys.Made` (`SshTestKeys.cs` line 74) via
  `Dsa1024Key = new(() => Made(DSA.Create(1024)))` (line 16).
- Because the fast tests fail, every later macOS step is skipped, including BL-324's "Record
  the non-ASCII NTLM password login", so BL-352 cannot get its recording until this is fixed.
- Fix: build `Dsa1024` with `DSA.Create()` + `ImportParameters` (or `ImportPkcs8PrivateKey`)
  from a fixed 1024-bit key held in `SshTestKeys.cs` (generate it once on Windows or Linux
  with PowerShell and paste the bytes). Import is supported on macOS.

## Acceptance criteria

- [ ] `SshTestKeys.Dsa1024` is imported from fixed parameters; no test in
      `Surl.Protocol.Ssh.UnitTests` calls `DSA.Create(<keySize>)` or otherwise generates a DSA key.
- [ ] `dotnet build` is clean and the fast tests are green on Windows.
- [ ] The CI "Fast tests" step passes on macOS for the commit that carries the change
      (or, if CI has not run it yet, no remaining `DSA.Create(` with a key size exists in the repository's tests).

## Notes

## Log

- 2026-10-01: Created.
