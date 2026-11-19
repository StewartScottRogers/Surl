---
id: BL-1388
title: Find why an ssh-dss signature from a PKCS #8 DSA host key sometimes fails to verify on macOS, and fix it
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/FileTransfer/Surl.Protocol.Ssh.UnitLibrary, Protocols/FileTransfer/Surl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-11-19
completed:
---
# BL-1388 — Find why an ssh-dss signature from a PKCS #8 DSA host key sometimes fails to verify on macOS, and fix it

## Goal

An `ssh-dss` signature surl makes from any readable DSA host key always verifies, on Windows, Linux and macOS;
`SshHostKeyFileTests.Read_DsaKeyWithWeakAlgorithms_SignsSshDssThatTheKeyVerifies` never flakes.

## Context

- Failed once on osx-x64 in CI run 44455066966 ("Publish osx-x64", PR #110, 2026-11-19, attempt 1; passed on
  rerun): case `"PKCS #8 DSA"`, `Assert.IsTrue(SshTestKeys.Dsa1024.VerifyData(..., SHA1,
  IeeeP1363FixedFieldConcatenation))` at `Protocols/FileTransfer/Surl.Protocol.Ssh.UnitTests/SshHostKeyFileTests.cs:156`
  was false. 1 of 1,094 SSH tests.
- DSA's k is random, so each run signs differently. An intermittent verify failure in DSA is the classic
  sign of a value with a leading zero byte (r, s, or a key parameter such as x shorter than q) being mishandled
  somewhere - in how `SshHostKeyFile` reads or re-imports PKCS #8 parameters (the test builds the file with
  `Unsigned(...)` at `:138-142`), or in the platform's DSA on macOS.
- Signing: `Protocols/FileTransfer/Surl.Protocol.Ssh.UnitLibrary/SshDsaHostKey.cs:33-37`
  (`dsa.SignData(data, SHA1, IeeeP1363FixedFieldConcatenation)`). This is a real client-visible risk: upstream
  curl would reject the host key signature and fail the handshake now and then.

## Acceptance criteria

- [ ] A named fast test signs with each readable DSA key case at least 2,000 times and verifies every signature
      (it is how a lane on Windows can hunt the fault); if it fails on Windows, the cause is found and fixed in
      `Surl.Protocol.Ssh.UnitLibrary`.
- [ ] The cause is written in this task's Log, with the parameter or value that triggered it. If it does not
      reproduce on Windows, the Log says so and the task reads the key-import path for leading-zero handling
      (each parameter padded to its expected length before `ImportParameters`), fixes any gap found, and
      adds a test pinning a key whose x has a leading zero byte.
- [ ] If the fault is in macOS's platform DSA itself, the fix is surl's own hand-built DSA signing for
      `ssh-dss` (BCL-only rule: hand-build, never a package), isolated and held to the quality gates.
- [ ] `dotnet build` is clean, fast tests green, `Surl.Protocol.Ssh` stays at 100/100 coverage.
- [ ] A Release or CI run on macOS passes the test (dispatch a workflow with `dispatch-workflow.ps1` if needed;
      park as Blocked "Waiting for run:<id>", never on Stewart).

## Notes

- Keep the many-iterations test fast (well under a second per case on Windows), or mark it `Integration`.

## Log

- 2026-11-19: Created.
