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
completed:
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

- [ ] A fast test in `Surl.Protocol.Ssh.UnitTests` (no `TestCategory=Integration`) reproduces
      the root cause, for example with a fixed DH private value (through `ISshRandomSource`)
      that gives a K, f or RSA signature with a leading zero byte. It fails before the fix
      and passes after it. Notes name the test and the root cause.
- [ ] The fix follows RFC 4251 section 5, RFC 4253, RFC 4419 and RFC 8332 as they apply.
      Notes cite the section the old code broke.
- [ ] By hand, at least 1000 sftp downloads by the pinned Windows build against surl, run
      against the fixed code, have no failure. Notes record the command, the count and the
      result. Alternatively, if the cause is libssh2's own WinCNG defect, Notes record that
      evidence, and a `docs` task for the ADR is filed and named there.
- [ ] `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is clean. `dotnet test --filter
      "TestCategory!=Integration"` is green. The library keeps 100% line and branch coverage
      and every method stays at complexity 10 or less.

## Notes

## Log

- 2026-09-30: Created.
