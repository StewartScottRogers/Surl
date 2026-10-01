---
id: BL-253
title: Record BL-172's measured SSH exchanges with the pinned builds in ADR-0051 and ADR-0054
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-172]
touches: [Documentation/Planning/Decisions, Documentation/Product/Requirements.md]
requirement: FR-042
created: 2026-09-30
completed: 2026-09-30
---
# BL-253 — Record BL-172's measured SSH exchanges with the pinned builds in ADR-0051 and ADR-0054

## Goal

ADR-0051 and ADR-0054 state what the pinned upstream curl 8.21.0 builds actually did against
surl over scp and sftp, as BL-172 measured it. ADR-0051's Context no longer holds a guess the
measurement contradicts. FR-039, FR-040 and FR-042 agree with both ADRs.

## Context

- BL-172 proved that pinned upstream curl transfers files with surl over scp and sftp. It
  could not edit `Documentation/Planning/Decisions`, which was outside its touches. Every fact
  below comes from BL-172's Notes, so this task depends on BL-172. Read the Notes there
  (`Tasks/Done/.../BL-172-*.md` or its archive), and where they differ from this list, the
  Notes win. The tests that pin the facts are in `Surl.Conformance.UnitTests`:
  - `UpstreamCurlLogsInToSurlOverSshTests.cs`
  - `UpstreamCurlOffersSshAlgorithmsTests.cs`
  - `UpstreamCurlTransfersFilesWithSurlOverScpTests.cs`
  - `UpstreamCurlTransfersFilesWithSurlOverSftpTests.cs`
- Facts to record, all measured with the Windows reference build (curl 8.21.0, libssh2 1.11.1,
  WinCNG) unless marked otherwise:
  1. A refused login writes stderr `curl: (67) Login denied`. ADR-0051's Context ("What curl
     does with the host key and the login", around line 156) guessed "Authentication failure".
  2. A `--hostpubsha256` mismatch writes
     `curl: (60) Denied establishing ssh session: mismatch SHA256 fingerprint. Remote <base64>
     is not equal to <pinned>`, followed by curl's sslcerts help text.
  3. A host key that is missing from `--knownhosts`, or different there, is exit 60.
  4. With no pin and no known_hosts file, the exit is 2.
  5. When surl holds only an ECDSA or only an Ed25519 host key, the Windows (WinCNG) build
     exits 2 with `Failure establishing ssh session: -5, Unable to exchange encryption keys`.
     The OpenSSL builds (Linux and macOS pins) complete the transfer.
  6. The negotiated algorithms:
     - Windows: `diffie-hellman-group-exchange-sha256`, `rsa-sha2-512`,
       `chacha20-poly1305@openssh.com`, compression `none` (or `zlib` with
       `--compressed-ssh`), strict kex on.
     - OpenSSL builds: `curve25519-sha256`, with the same host-key, cipher and compression
       choices. With an ECDSA or Ed25519 host key, the host key's own algorithm is used.
  7. The Linux and macOS builds' `KEXINIT` lists, exactly as BL-172's Notes give them.
- ADR-0051 decision 2's opening paragraph already predicts the Windows negotiation. Record the
  measurement beside it rather than repeating it. Add the OpenSSL lists to ADR-0051's Context,
  "What upstream curl 8.21.0 sends (measured)". Add the transfer-level facts (1 to 5 as they
  bear on scp and sftp) to ADR-0054 as an amendment section, as BL-229 and BL-231 did. Mark
  each addition "Recorded from BL-172's measurement". It is a record, not a new decision.
- The four OpenSSL-only names that no row covers (`blowfish-cbc`, `cast128-cbc`,
  `hmac-ripemd160`, `hmac-ripemd160@openssh.com`) are decided by BL-252. Here, only list them
  as measured.
- BL-249 also edits ADR-0051 (a separate amendment section). Keep the two sections apart.
- `Documentation/Product/Requirements.md` rows FR-039, FR-040 and FR-042: change a row only
  where it states one of these facts, or contradicts one.
- Never use the Curl port as evidence (ADR-0003).

## Acceptance criteria

- [x] ADR-0051's Context says a refused login is `curl: (67) Login denied`. The text
      "Authentication failure" is gone, or marked as the guess that was replaced.
- [x] ADR-0051 records, citing BL-172, each of these:
      - the exit-60 fingerprint-mismatch text
      - the `--knownhosts` exit 60 and the no-pin exit 2
      - the WinCNG exit 2 (-5) for ECDSA-only and Ed25519-only host keys, and the OpenSSL
        builds' success in those cases
      - the negotiated algorithms per platform
      - the Linux and macOS `KEXINIT` lists
- [x] ADR-0054 has an amendment section that records the scp and sftp outcomes BL-172
      measured, and it cites BL-172.
- [x] FR-039, FR-040 and FR-042 in `Documentation/Product/Requirements.md` contradict neither
      ADR. Notes say which rows changed, or that none needed to.
- [x] Every statement added is true of BL-172's Notes and of the repository.

## Notes

- **ADR-0051**, every addition marked "Recorded from BL-172's measurement":
  - Context, "What upstream curl 8.21.0 sends (measured)": a new bullet with the Linux and macOS
    builds' OpenSSL `KEXINIT` lists as BL-172's Notes give them, how they were recorded (the
    supplementary static-curl Windows build `589C8E4D...` as a predictor, ADR-0017) and that
    `KexInit_LinuxAndMacOSBuilds_ListTheOpenSslAlgorithms` pins them on CI; the four names no row
    covered point to BL-252 and ADR-0061.
  - Context, "What curl does with the host key and the login": a new bullet with the measured
    host-key outcomes (the exit-60 mismatch text, `--knownhosts` empty or holding another key 60,
    no pin 2, `--hostpubmd5` and `-k` transfer); "Authentication failure" is kept only as "a guess
    the measurement replaced", beside the measured `curl: (67) Login denied`; a new bullet on the
    measured password and public-key logins.
  - Decision 2: a "What was negotiated" block after the opening paragraph - the Windows build's
    measured negotiation, the WinCNG exit 2 (-5) for ECDSA-only and Ed25519-only host keys, the
    OpenSSL builds' negotiation marked predicted (pinned for CI, not yet measured), the algorithms
    no run negotiated, and the intermittent -8 failure (BL-251). Kept apart from Amendment 1
    (BL-249).
- **ADR-0054**: an "Amended by" header line and "Amendment 1 - What the pinned build did over scp
  and sftp (BL-172, recorded by BL-253, 2026-09-30)": every decision 16 row matched first time,
  BL-172's harness choices (the `-Q pwd` row's "no request" is not observable), the refused login
  67 over both schemes, the host-key outcomes (measured over `sftp://` only), the EC-only host key
  on Windows and the intermittent failure.
- **Requirements.md:** no row changed. FR-039, FR-040 and FR-042 neither state nor contradict any
  of these facts.
- **Where BL-172's Notes differ from this task's list:** fact 5's and fact 6's OpenSSL outcomes
  (the transfer completing with an ECDSA-only or Ed25519-only key, and `curve25519-sha256`) are
  predictions pinned for the Linux and macOS CI legs, not measurements, so both ADRs say
  "predicted". Fact 3 and fact 4 were measured over `sftp://` only.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ADR-0051 and ADR-0054 record BL-172's measured scp/sftp exchanges with the pinned builds
