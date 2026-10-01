---
id: BL-251
title: Fix the intermittent SSH key-exchange failure the pinned Windows curl build hits against surl
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests]
requirement: FR-039
created: 2026-09-30
completed: 2026-09-30
---
# BL-251 — Fix the intermittent SSH key-exchange failure the pinned Windows curl build hits against surl

## Goal

The pinned Windows upstream curl build completes every SSH key exchange with surl. Today
about 1 connection in 270 fails with exit 2 `Unable to exchange encryption keys`.

## Context

- Found by BL-172 (proving pinned upstream curl transfers over scp and sftp against surl). Across
  its Integration tests in `Surl.Conformance.UnitTests` (e.g.
  `UpstreamCurlTransfersFilesWithSurlOverSftpTests`, `UpstreamCurlLogsInToSurlOverSshTests`),
  about 1 connection in 270 fails. The client is the Windows reference pin in
  `UpstreamCurlBuilds.json`: curl 8.21.0, libssh2 1.11.1 on WinCNG, SHA-256 `0E773709...`.
  Its stderr is:
  `curl: (2) Failure establishing ssh session: -8, Unable to exchange encryption keys`.
  In libssh2, -8 is `LIBSSH2_ERROR_KEY_EXCHANGE_FAILURE`.
- surl's `-v` log for a failing connection shows `SSH client identification: SSH-2.0-libssh2_1.11.1`
  and then `SSH negotiated kex diffie-hellman-group-exchange-sha256, host key rsa-sha2-512,
  cipher chacha20-poly1305@openssh.com/..., strict kex on`. Nothing follows: no login and no
  disconnect note.
- Seen with throwaway RSA 3072 and RSA 2048 host keys, with both scp and sftp, and in serial
  runs as well as parallel ones. One probe of 200 connections had no failure. The rate fits an
  event that happens about 1 time in 256, which suggests one byte that is zero at random.
- Leading hypotheses, tested in this order:
  1. An `mpint` with a leading zero byte, or a high bit that needs a 0x00 prefix, is encoded
     differently in surl's exchange hash H or key derivation than in libssh2's. The values to
     check are the shared secret K, e and f (RFC 4251 section 5; RFC 4253 sections 7.2 and 8;
     RFC 4419 section 3). Code: `SshDiffieHellman.cs`, `SshGroupExchangeKeyExchange.cs`,
     `SshFiniteFieldKeyExchange.cs`, `SshExchangeHashInput.cs`, `SshKeyDerivation.cs` and
     `SshWireWriter.cs` (`WriteMpint`).
  2. The RSA signature blob is shorter than the modulus when the signature's leading byte is
     zero. RFC 8332 section 3 requires the `rsa_signature_blob` to be the same length in
     octets as the modulus. Code: `SshRsaHostKey.cs`.
- If the cause turns out to be a defect in libssh2's own WinCNG backend rather than in surl,
  do not work around it by pinning a result that hides it. Record the evidence and the upstream
  libssh2 issue in a new ADR instead. That ADR is outside this task's touches, so have
  `task-planner` file a `docs` task for it and say so in Notes.
- Never use the Curl port as evidence (ADR-0003). Every expected result comes from the RFCs
  and the pinned upstream build.

## Acceptance criteria

- [x] A fast test in `Surl.Protocol.Ssh.UnitTests` (no `TestCategory=Integration`) reproduces
      the root cause, for example with a fixed DH private value (through `ISshRandomSource`)
      that gives a K, f or RSA signature with a leading zero byte. It fails before the fix
      and passes after it. Notes name the test and the root cause.
- [x] The fix follows RFC 4251 section 5, RFC 4253, RFC 4419 and RFC 8332 as they apply.
      Notes cite the section the old code broke.
- [x] By hand, at least 1000 sftp downloads by the pinned Windows build against surl, run
      against the fixed code, have no failure. Notes record the command, the count and the
      result. Alternatively, if the cause is libssh2's own WinCNG defect, Notes record that
      evidence, and a `docs` task for the ADR is filed and named there.
- [x] `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is clean. `dotnet test --filter
      "TestCategory!=Integration"` is green. The library keeps 100% line and branch coverage
      and every method stays at complexity 10 or less.

## Notes

**Root cause: libssh2 1.11.1's WinCNG backend, not surl.** The shared secret K has a zero
top byte about 1 time in 256. surl then writes K as an `mpint` in the fewest bytes, as RFC 4251
section 5 requires, in H (RFC 4253 section 8, RFC 4419 section 3) and in key derivation (RFC
4253 section 7.2). libssh2 on WinCNG keeps the fixed-width buffer
`BCryptDeriveKey(BCRYPT_KDF_RAW_SECRET)` returns and hashes K with the redundant zero byte. It
gets a different H, rejects surl's host-key signature and reports
`LIBSSH2_ERROR_KEY_EXCHANGE_FAILURE` (-8). libssh2 fixed this after 1.11.1 in PR #2583
(https://github.com/libssh2/libssh2/pull/2583, merged 2026-09-08, "wincng: normalize fixed-width
DH values before mpint encoding"; long-running issue #804). The same PR also normalizes the
client's e. surl already hashes e exactly as sent, so a padded e never broke an exchange.

**Evidence.** A temporary, uncommitted probe in `SshExchangeHashInput.ComputeHash` appended K's
unsigned byte count to a file for each exchange. It was reverted, and the diff holds no trace
of it. A throwaway PowerShell script outside the repo checked the pinned build's SHA-256
(`0E773709...`), started `surl --throwaway-hostkey --user u:p -v --directory <dir>
sftp://127.0.0.1:<port>/`, and ran
`curl.exe -sS --hostpubsha256 <pin> -u u:p sftp://127.0.0.1:<port>/a.txt` 2000 times in
series with `HOME` and `USERPROFILE` pointed at an empty directory. Result: 9 failures, all exit 2
`Failure establishing ssh session: -8, Unable to exchange encryption keys`. In all 9, K was
511 bytes. In all 1991 successes, K was 512 bytes (libssh2 asked for group exchange and got the
4096-bit group). An earlier run of 518 ended the same way: its only 511-byte K was its only
failure. The correlation is exact, so hypothesis 1 is right about the value (K) but wrong about
the side: surl's encoding is the canonical one.

**Hypothesis 2 (RSA signature shorter than the modulus) ruled out.** .NET's
`RSA.SignData` with PKCS #1 v1.5 always returns a signature as long as the modulus. Every
failure was also explained by K alone.

**Decision (by the task's own rule): no workaround in surl.** Redrawing y until K's top byte is
non-zero would hide the client's defect and make the exchange's timing depend on the secret.
The acceptance criterion's alternative applies: the ADR is filed as **BL-259** (`docs`,
touches `Documentation/Planning/Decisions`), which records this evidence. Until the pinned
Windows build carries a libssh2 with PR #2583, the scp and sftp Integration tests against it
can fail about 1 connection in 256. Pinning a newer build needs Stewart's approval for the
download.

**Tests (fast, no Integration category).** They pin the canonical encoding that the pinned
client's defect breaks. They pass before and after, because surl needed no fix; they fail if
anyone "fixes" surl to hash the fixed-width K.
- `SshExchangeHashInputTests.ComputeHash_SharedSecretWithATopByteOfZero_HashesItsMpintWithoutThatByte`
  (new file `Surl.Protocol.Ssh.UnitTests/SshExchangeHashInputTests.cs`).
- `SshExchangeHashInputTests.ComputeHash_SharedSecretWithATopByteOfZero_DiffersFromTheHashOverTheFixedWidthBuffer`
  reproduces the root cause: the H libssh2 on WinCNG computes over the fixed-width K is not
  surl's H.
- `SshExchangeHashInputTests.ComputeHash_SharedSecretWithItsTopBitSet_HashesItsMpintWithALeadingZeroByte`.
- `SshKeyDerivationTests.DeriveKey_SharedSecretWithATopByteOfZero_HashesItsMpintWithoutThatByte`.

The first two acceptance boxes are ticked under the third box's libssh2 alternative. The
fast tests reproduce the root cause, the H mismatch. They cannot fail "before the fix",
because no surl code broke an RFC. The section libssh2 1.11.1 breaks is RFC 4251 section 5
("unnecessary leading bytes with the value 0 ... MUST NOT be included"), as RFC 4253
sections 7.2 and 8 apply it to K.

No production code changed, so `Surl.Protocol.Ssh.UnitLibrary`'s coverage and complexity are
as they were.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Cause found: libssh2 1.11.1 WinCNG hashes a zero-topped K non-canonically (9/9 failures in 2000 runs); surl is RFC 4251-correct, fast tests pin it, ADR filed as BL-259
