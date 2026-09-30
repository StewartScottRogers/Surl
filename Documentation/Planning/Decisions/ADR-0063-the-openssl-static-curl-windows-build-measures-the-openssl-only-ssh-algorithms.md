# ADR-0063 — The OpenSSL static-curl Windows build measures the OpenSSL-only SSH algorithms

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-263 (FR-039). No build was downloaded: the build this ADR uses was already pinned by
  ADR-0030.
- **Amends:** [ADR-0030](ADR-0030-static-curl-8-21-0-windows-build-as-a-supplementary-build-for-smb.md)'s and
  [ADR-0042](ADR-0042-negotiate-is-proved-with-the-unpatched-8-21-0-windows-build.md)'s scope of
  the supplementary build `C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe`, under the
  rule of [ADR-0017](ADR-0017-curl-se-8-22-0-windows-build-as-a-supplementary-build.md) that a supplementary build is
  used only for what its ADR names.

## Context

ADR-0061 gave surl four SSH algorithm names that only upstream curl's OpenSSL builds offer:
`blowfish-cbc`, `cast128-cbc`, `hmac-ripemd160` and `hmac-ripemd160@openssh.com`, all behind
`--allow-weak-ssh-algorithms`. BL-261 has to prove that a pinned upstream curl completes an SFTP
transfer against surl with each of them agreed.

The Windows reference pin (Git for Windows, libssh2 1.11.1 on WinCNG) does not offer any of the
four (`UpstreamCurlOffersSshAlgorithmsTests.KexInit_WindowsBuild_ListsTheWinCngAlgorithms`). The
Linux and macOS reference pins do (`KexInit_LinuxAndMacOSBuilds_ListTheOpenSslAlgorithms`), but
neither is installed on Stewart's Windows machine, where the dark factory's lanes run their tests,
so a test that needs them would never run there.

A third Windows pin is already on that machine: stunnel/static-curl's Windows build of tag
`curl-8_21_0`, unpatched, libssh2 1.11.1 on OpenSSL 4.0.1 - the same curl source, libssh2 and
OpenSSL release as the Linux and macOS reference pins (ADR-0016), pinned by SHA-256 as a
supplementary build for SMB (ADR-0030) and HTTP Negotiate (ADR-0042).

### Measured

With the `ClientKexInitRecorder` of `UpstreamCurlOffersSshAlgorithmsTests`, the build's
`sftp://` `SSH_MSG_KEXINIT` is identical, list for list, to the Linux and macOS reference pins':
identification `SSH-2.0-libssh2_1.11.1`, and the same kex, host-key, cipher and MAC lists in the
same order, `blowfish-cbc` and `cast128-cbc` among the ciphers and both RIPEMD-160 MACs last.
`KexInit_OpenSslWindowsBuild_ListsTheOpenSslAlgorithms` pins it.

A throwaway, uncommitted measurement then ran the build against an in-process `surl sftp://`
whose offer had been narrowed by hand to one cipher and one MAC, downloading a 6-byte file with
`-v`:

| surl offered | curl's verbose line | curl's exit code |
| --- | --- | --- |
| `aes128-ctr`, `hmac-sha2-256` | `cipher aes128-ctr`, `MAC hmac-sha2-256` | 0, the file's bytes |
| `aes128-ctr`, `hmac-ripemd160` | `MAC hmac-ripemd160/hmac-ripemd160` | 0, the file's bytes |
| `aes128-ctr`, `hmac-ripemd160@openssh.com` | `MAC hmac-ripemd160@openssh.com/...` | 0, the file's bytes |
| `blowfish-cbc`, `hmac-sha2-256` | `cipher blowfish-cbc/blowfish-cbc` | -1073741819 (0xC0000005, access violation) |
| `cast128-cbc`, `hmac-sha2-256` | `cipher cast128-cbc/cast128-cbc` | -1073741819 (0xC0000005, access violation) |
| `blowfish-cbc` or `cast128-cbc`, with `OPENSSL_CONF` activating the `default` and `legacy` providers | as above | 0, the file's bytes |

So libssh2 1.11.1 offers Blowfish and CAST-128 because OpenSSL 4 still declares them, but
OpenSSL 3 and later keep both in the `legacy` provider, which libssh2 never loads: once they are
agreed, curl crashes as the keys are installed. The legacy provider is compiled into this static
build, and an OpenSSL configuration file named by `OPENSSL_CONF` that activates it (with
`default`, which activating any provider otherwise unloads) makes both ciphers work.
RIPEMD-160 is in OpenSSL's `default` provider and needs nothing.

## Decision

1. **The supplementary build is also admitted for SSH measurements of the four OpenSSL-only
   algorithms** of ADR-0061 - `blowfish-cbc`, `cast128-cbc`, `hmac-ripemd160` and
   `hmac-ripemd160@openssh.com` - and for pinning the `KEXINIT` it sends, which those
   measurements rest on. Its `origin` in `UpstreamCurlBuilds.json` says so. Every other SSH
   measurement on Windows stays with the reference build (ADR-0017: a supplementary build never
   second-guesses a reference build).
2. **Why this build:** it is the only pinned Windows build that offers the four, and it is the same
   curl, libssh2 and OpenSSL as the Linux and macOS reference pins, so what it measures is what
   those pins would measure; the Linux and macOS legs of CI still run their own reference pins.
3. **Blowfish and CAST-128 are proved with the `legacy` provider loaded.** A test that agrees
   `blowfish-cbc` or `cast128-cbc` with this build (and with the Linux and macOS reference pins,
   the same OpenSSL) runs curl with `OPENSSL_CONF` pointing at a temporary configuration file that
   activates the `default` and `legacy` providers, and says so beside the test. This is a
   documented OpenSSL setting of the client's own, not a change to curl: without it, no build of
   upstream curl 8.21.0 on OpenSSL 4 can use either cipher against any server, which is
   upstream's behaviour to record, not surl's to fix. The RIPEMD-160 MACs are proved without it.
4. **surl does not change.** It keeps offering the four behind `--allow-weak-ssh-algorithms`
   (ADR-0061): a client that has the legacy provider, or a libssh2 on another crypto backend, can
   use them.

## Alternatives considered

- **Install the Linux reference pin under WSL and run BL-261 there.** Rejected: the lanes run
  their tests on Windows, and installing a build is Stewart's approval, while this build is
  already pinned.
- **Pin another Windows build.** Rejected: it needs a download Stewart must approve, and this one
  is already like for like with the Linux and macOS pins.
- **Prove Blowfish and CAST-128 only as far as the crash.** Rejected: an access violation proves
  nothing about surl's side of the exchange; with the legacy provider the whole transfer runs and
  checks surl's cipher against OpenSSL's.

## Consequences

- `UpstreamCurlBuilds.json`'s third Windows entry names SSH in its `origin`.
- `PinnedUpstreamCurl.RunSupplementaryBuildWithEnvironmentAsync` runs a supplementary build with
  an environment, as the SSH tests need an `IsolatedCurlHome` (ADR-0051 decision 8).
- BL-261 runs on Windows with this build, and sets `OPENSSL_CONF` for its two cipher cases.
