# ADR-0062 — Surl keeps K's canonical `mpint` though libssh2 1.11.1 on WinCNG fails 1 key exchange in 256

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-259 (FR-039), from the cause BL-251 found.

## Context

The Windows reference pin in `UpstreamCurlBuilds.json` (Git for Windows' curl 8.21.0, SHA-256
`0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, libssh2 1.11.1 on the WinCNG
backend) now and then fails an scp or sftp transfer against surl with exit 2,
`curl: (2) Failure establishing ssh session: -8, Unable to exchange encryption keys`
(`LIBSSH2_ERROR_KEY_EXCHANGE_FAILURE`).

**Measurement (BL-251).** A throwaway PowerShell script checked the pinned build's SHA-256,
started `surl --throwaway-hostkey --user u:p -v --directory <dir> sftp://127.0.0.1:<port>/` and
ran `curl.exe -sS --hostpubsha256 <pin> -u u:p sftp://127.0.0.1:<port>/a.txt` 2000 times in
series, with `HOME` and `USERPROFILE` pointed at an empty directory. A temporary probe in
`SshExchangeHashInput.ComputeHash`, reverted afterwards, logged the byte length of each shared
secret K. libssh2 asked for group exchange and got the 4096-bit group every time.

| Outcome | Runs | K's unsigned length |
| --- | --- | --- |
| Succeeded | 1991 | 512 bytes, every one |
| Failed, exit 2, `-8` | 9 | 511 bytes, every one |

An earlier run of 518 ended the same way: its one 511-byte K was its one failure. The
correlation is exact, and 9 in 2000 matches the 1 in 256 chance that a uniformly distributed K
has a zero top byte.

**Cause.** RFC 4251 section 5 says an `mpint`'s "unnecessary leading bytes with the value 0 or
255 MUST NOT be included", and RFC 4253 sections 7.2 and 8 (and RFC 4419 section 3 for group
exchange) hash K as an `mpint`. surl writes K in the fewest bytes. libssh2 1.11.1's WinCNG
backend keeps the fixed-width buffer `BCryptDeriveKey(BCRYPT_KDF_RAW_SECRET)` returns and hashes
K with the redundant zero byte, so it computes a different exchange hash H, finds surl's
host-key signature over H invalid and gives up. libssh2 fixed this after 1.11.1 in PR #2583,
"wincng: normalize fixed-width DH values before mpint encoding"
(https://github.com/libssh2/libssh2/pull/2583, merged 2026-09-08), which closes the
long-running issue https://github.com/libssh2/libssh2/issues/804. The same PR also normalizes
the client's e; surl hashes e exactly as the client sent it, so a padded e never broke an
exchange.

A second hypothesis, an RSA signature shorter than the modulus, is ruled out: .NET's
`RSA.SignData` with PKCS #1 v1.5 always returns a signature as long as the modulus, and K's
length alone explained every failure.

## Decision

1. **Surl keeps hashing K as the canonical `mpint`.** RFC 4251 section 5 is a MUST, and every
   other client (and libssh2 itself once PR #2583 ships) computes H that way. BL-251's fast
   tests in `Surl.Protocol.Ssh.UnitTests` pin it and fail if anyone "fixes" surl to hash the
   fixed-width K:
   `SshExchangeHashInputTests.ComputeHash_SharedSecretWithATopByteOfZero_HashesItsMpintWithoutThatByte`,
   `SshExchangeHashInputTests.ComputeHash_SharedSecretWithATopByteOfZero_DiffersFromTheHashOverTheFixedWidthBuffer`,
   `SshExchangeHashInputTests.ComputeHash_SharedSecretWithItsTopBitSet_HashesItsMpintWithALeadingZeroByte`
   and `SshKeyDerivationTests.DeriveKey_SharedSecretWithATopByteOfZero_HashesItsMpintWithoutThatByte`.
2. **No workaround.** Surl does not redraw its private exponent until K has a non-zero top
   byte, and does not steer libssh2 away from the finite-field exchanges. Redrawing would hide
   the client's defect from the one place meant to catch it, make the exchange's timing depend
   on the secret, and change behaviour every correct client sees. Surl's bar is the protocol's
   specification and upstream curl's own behaviour (ADR-0003); a client defect upstream has
   already fixed is not a behaviour to mate.
3. **The expected flake rate.** Every scp and sftp test in `Surl.Conformance.UnitTests` marked
   `[TestCategory("Integration")]` that runs the Windows reference pin
   (`UpstreamCurlTransfersFilesWithSurlOverScpTests`, `UpstreamCurlTransfersFilesWithSurlOverSftpTests`,
   and every other test that makes that build finish an SSH key exchange with surl) can fail
   about **1 connection in 256** (0.39%; BL-251 measured 9 in 2000, 0.45%) with exit 2 and
   `Unable to exchange encryption keys`. That failure, and only that one, is this defect; a
   re-run passes. Any other exit code or message is a real failure. A test that makes n SSH
   connections fails with probability about 1 - (255/256)^n.
4. **What clears it.** A Windows reference pin whose libssh2 carries PR #2583 (a libssh2
   release after 1.11.1). Pinning that build is a new ADR, and downloading it needs Stewart's
   approval (root `CLAUDE.md`); until then the rate in decision 3 stands. The Linux and macOS
   reference pins use libssh2 on OpenSSL, not WinCNG, and were not part of this measurement.

## Consequences

- An intermittent exit 2 with `-8, Unable to exchange encryption keys` from the Windows
  reference pin is explained, and needs no new task unless its rate is clearly above 1 in 256.
- Surl's key exchange stays RFC 4251-correct and constant in shape for every client.
- Never cited as evidence here: the Curl port (ADR-0003).
