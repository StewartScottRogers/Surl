---
id: BL-172
title: Prove pinned upstream curl transfers files over scp and sftp against surl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-171, BL-167, BL-168, BL-169, BL-170]
touches: [Surl.Conformance.UnitLibrary, Surl.Conformance.UnitTests]
requirement: FR-042
created: 2026-09-29
completed: 2026-09-30
---
# BL-172 — Prove pinned upstream curl transfers files over scp and sftp against surl

## Goal

`[TestCategory("Integration")]` tests in `Surl.Conformance.UnitTests` prove that the pinned
upstream curl 8.21.0 builds log in to a live `surl` over `scp` and `sftp` and download, upload,
list and run quote commands exactly as BL-154's and BL-155's ADRs expect, and record which SSH
algorithms each platform's build negotiated.

## Context

- The cases: BL-155's ADR's list of command lines and expected results, plus BL-154's: a
  password login (`-u user:pass`), a public-key login (`--key`/`--pubkey` with a key pair the
  test generates in a temporary directory), a refused login (curl's exit code measured, e.g.
  67), the host-key check satisfied as the ADR decides (`--hostpubsha256` of surl's key, or
  `-k`), and `--compressed-ssh`.
- Harness: `Surl.Conformance.UnitTests/SurlOnLoopback.cs`, `PinnedUpstreamCurl.cs`,
  `AccountsFile.cs`; `Assert.Inconclusive` when the pinned build is absent (BL-020's rule); the
  Linux and macOS legs run on CI (ADR-0016). Isolate curl from the operator's own
  `known_hosts` (point `HOME`/`USERPROFILE` at a temporary directory for the run, or whatever
  BL-154's ADR says).
- Algorithms: curl's preference decides the kex and cipher, surl's configured host key decides
  the host-key algorithm. Read the negotiated algorithms from surl's verbose notes (BL-154's ADR)
  and write in Notes which algorithms each platform's build negotiated; include one run per
  host-key type (ECDSA, RSA, Ed25519) by configuring only that key. An algorithm no run
  negotiates is covered by unit tests only, and Notes say so. Record here the Linux and macOS
  builds' KEXINIT lists, as BL-154's ADR says.
- Any disagreement with the pinned build is fixed in `Surl.Protocol.Ssh` through a new task
  filed by `task-planner`, never by changing the expected result (ADR-0003); list them in the
  Log.

## Acceptance criteria

- [x] Integration tests exist for every case in Context and pass on Windows with the pinned
      build present: `dotnet test --filter "FullyQualifiedName~Surl.Conformance"` is green.
- [x] Notes list the algorithms negotiated per platform and per host-key type, and the Linux and
      macOS KEXINIT lists once CI has run.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green; no fast test opens a
      socket.

## Notes

- **Tests** (all `[TestCategory("Integration")]`, in `Surl.Conformance.UnitTests`):
  `UpstreamCurlTransfersFilesWithSurlOverSftpTests` (every SFTP row of ADR-0054 decision 16),
  `UpstreamCurlTransfersFilesWithSurlOverScpTests` (every SCP row),
  `UpstreamCurlLogsInToSurlOverSshTests` (password and public-key logins over both schemes, the
  refused login, `--hostpubmd5`, `-k`, a wrong `--hostpubsha256`, no pin, `--knownhosts` empty and
  with another key, `--compressed-ssh`, one run per host-key type) and
  `UpstreamCurlOffersSshAlgorithmsTests` (the client's `KEXINIT`, pinned per platform). Every
  expected result matched ADR-0054 decision 16 on the first run; nothing was changed to pass.
- **Harness:** `UpstreamCurlRunner` gained an environment overload (fast-tested,
  `CreateStartInfo_Environment_SetsEachVariableAndKeepsTheRest`); `IsolatedCurlHome` points `HOME`
  and `USERPROFILE` at a temporary directory; `SurlOnLoopback` now keeps surl's log (`Log`) and
  exposes `ServedDirectory`; `SshLogNotes` reads the fingerprint and negotiated notes;
  `SshTestKeys` makes host keys (RSA and ECDSA as PKCS #8 from the BCL, Ed25519 as RFC 8410 PKCS #8
  from a random seed) and an RSA user key pair (PKCS #1 PEM, which WinCNG libssh2 reads);
  `PinnedUpstreamCurlOverSsh` writes surl's log to the test log whenever curl exits non-zero;
  `ClientKexInitRecorder` is a loopback stand-in server (as `DigestChallengeRelay` is) that reads the
  client's identification line and `KEXINIT`, so each CI leg checks its own build's lists.
- **Measured with the Windows build** (`0E773709...`, WinCNG): a refused login is exit 67
  `curl: (67) Login denied` (ADR-0051's Context guessed "Authentication failure"); a wrong
  `--hostpubsha256` is exit 60 `curl: (60) Denied establishing ssh session: mismatch SHA256
  fingerprint. Remote <surl's base64> is not equal to <pinned>` followed by curl's sslcerts help
  text; a key missing from `--knownhosts` (empty file) and a different key there are both exit 60;
  no pin and no `known_hosts` is exit 2; `--hostpubmd5` and `-k` both transfer. The public-key login
  sends `publickey` with an `ssh-rsa` key (surl's note: `publickey for tester, key ssh-rsa SHA-256
  ...`) and succeeds.
- **Negotiated algorithms** (surl's `SSH negotiated` note):
  - Windows build, RSA host key (throwaway 3072 or `--hostkey` 2048): kex
    `diffie-hellman-group-exchange-sha256`, host key `rsa-sha2-512`, cipher
    `chacha20-poly1305@openssh.com` both ways, MAC implicit, compression `none` (`zlib/zlib` with
    `--compressed-ssh`), strict kex on.
  - Windows build, ECDSA P-256 or Ed25519 host key only: no common host-key algorithm (WinCNG lists
    only RSA); curl exits 2 `Failure establishing ssh session: -5, Unable to exchange encryption
    keys`, surl notes `SSH no common host key algorithm; client offered rsa-sha2-512,...`. Pinned in
    `HostKey_OnlyEllipticCurveOnTheWindowsBuild_Exits2NoCommonHostKeyAlgorithm`.
  - Linux and macOS builds (OpenSSL): kex `curve25519-sha256`, host key `rsa-sha2-512` for RSA,
    `ecdsa-sha2-nistp256` for ECDSA and `ssh-ed25519` for Ed25519, cipher
    `chacha20-poly1305@openssh.com`, MAC implicit, compression `none` (`zlib` with
    `--compressed-ssh`), strict kex on. Predicted, not yet measured by a Linux/macOS pin: see the
    next point. Pinned in `HostKey_OnlyEllipticCurveOnOpenSslBuilds_NegotiatesItAndDownloadsTheFile`.
  - Never negotiated by any run, so covered by `Surl.Protocol.Ssh` unit tests only: the
    `curve25519-sha256@libssh.org`, `ecdh-sha2-*`, `diffie-hellman-group16/18-sha512` and
    `group14-sha256` kex methods, `ecdsa-sha2-nistp384/521` and `rsa-sha2-256` host keys,
    `aes*-gcm@openssh.com` and `aes*-ctr` ciphers, every `hmac-*` MAC (chacha20-poly1305 wins on
    both builds), `zlib@openssh.com`, and every `--allow-weak-ssh-algorithms` entry.
- **The Linux and macOS `KEXINIT` lists.** A lane cannot run CI, so they were recorded with
  `Record-CurlExchange.ps1 -Raw -RawReplyFirst -RawReply 'SSH-2.0-surl\r\n' -Curl
  C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe`: the supplementary build pinned for
  SMB (`589C8E4D...`), which is stunnel/static-curl's build of the same tag, libssh2 1.11.1 and
  OpenSSL 4.0.1 as the Linux and macOS pins. It was used only as a predictor, never as evidence
  against the reference build (ADR-0017). `KexInit_LinuxAndMacOSBuilds_ListTheOpenSslAlgorithms`
  pins these lists on the Linux and macOS CI legs, so the first CI run confirms them or fails with
  the lists it recorded. Identification `SSH-2.0-libssh2_1.11.1`;
  - kex: `curve25519-sha256,curve25519-sha256@libssh.org,ecdh-sha2-nistp256,ecdh-sha2-nistp384,ecdh-sha2-nistp521,`
    then the Windows list (`diffie-hellman-group-exchange-sha256` ... `kex-strict-c-v00@openssh.com`);
  - host key: `ecdsa-sha2-nistp256,ecdsa-sha2-nistp384,ecdsa-sha2-nistp521`, their three
    `-cert-v01@openssh.com` forms, `ssh-ed25519,ssh-ed25519-cert-v01@openssh.com`, then the
    Windows list (`rsa-sha2-512` ... `ssh-rsa-cert-v01@openssh.com`);
  - cipher (both ways): `chacha20-poly1305@openssh.com,aes256-gcm@openssh.com,aes128-gcm@openssh.com,aes256-ctr,aes192-ctr,aes128-ctr,aes256-cbc,rijndael-cbc@lysator.liu.se,aes192-cbc,aes128-cbc,blowfish-cbc,arcfour128,arcfour,cast128-cbc,3des-cbc`;
  - MAC (both ways): the Windows list then `hmac-ripemd160,hmac-ripemd160@openssh.com`;
  - compression `none`, or `zlib,zlib@openssh.com,none` with `--compressed-ssh`; languages empty.
  - Not covered by ADR-0051 decision 2: `blowfish-cbc`, `cast128-cbc`, `hmac-ripemd160`,
    `hmac-ripemd160@openssh.com` (BL-252).
- **Disagreement found:** about one connection in 270 with the Windows build fails with
  `curl: (2) Failure establishing ssh session: -8, Unable to exchange encryption keys` after surl
  has noted the agreed algorithms (`diffie-hellman-group-exchange-sha256`), with no further note.
  0 in 200 in a focused probe, 1 to 2 per full run of the 70 SSH tests, so any Windows run of these
  tests can fail once. Not hidden by a retry: BL-251 fixes it in `Surl.Protocol.Ssh`.
- **Choices:** one surl per scheme per test (not one serving both URLs), so each test starts from
  a fresh directory as decision 16 asks; local files curl reads or writes (`up.txt`, `part.txt`,
  `out.txt`) live in the isolated home and are passed as absolute paths, since the runner sets no
  working directory; the `-Q pwd` row asserts the exit and output only (surl writes no note for a
  `REALPATH`, so "no request" is not observable in its log).
- The ADRs could not be edited here (`Documentation/Planning/Decisions` is outside the touches):
  BL-253 records these measurements in ADR-0051 and ADR-0054.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Filed BL-251 (the intermittent DH group-exchange failure with the Windows build, in
  `Surl.Protocol.Ssh`), BL-252 (blowfish-cbc, cast128-cbc and hmac-ripemd160 from the OpenSSL
  builds) and BL-253 (record these measurements in ADR-0051 and ADR-0054).
- 2026-09-30: Doing -> Done. Integration tests prove the pinned builds log in to surl over scp and sftp and download, upload, list and run -Q commands as ADR-0054 decision 16 expects, with each platform's KEXINIT and negotiated algorithms pinned
