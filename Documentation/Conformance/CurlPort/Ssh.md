# Phase 7 findings: SCP and SFTP

What `CurlPortBesideUpstreamCurlOverSshTests` in `Conformance/Surl.Conformance.UnitTests` found when it
ran every SCP and SFTP conversation of `UpstreamCurlTransfersFilesWithSurlOverScpTests`,
`UpstreamCurlTransfersFilesWithSurlOverSftpTests`, `UpstreamCurlLogsInToSurlOverSshTests`,
`UpstreamCurlOffersSshAlgorithmsTests` and `UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests` through
pinned upstream curl and the pinned Curl port, each against its own fresh `surl` and with its own empty
`HOME` and `USERPROFILE` (ADR-0089, ADR-0051 decision 8, BL-582).

## Measured

| What | Pin |
| --- | --- |
| Curl port | commit `b42c319ba940f66452b2b6f3e9910d46825b08fe`, `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7` (`CurlPortBuilds.json`, `win-x64`) |
| Upstream curl, reference | SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP` (the Windows reference build; every case but those below) |
| Upstream curl, OpenSSL | SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 OpenSSL/4.0.1 zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 c-ares/1.34.6 libidn2/2.3.8 libpsl/0.22.0 libssh2/1.11.1 nghttp2/1.69.0 ngtcp2/1.23.0 nghttp3/1.16.0` (the supplementary static-curl build of ADR-0063, paired with `KexInit_OpenSslWindowsBuild_ListsTheOpenSslAlgorithms` and every `UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests` case, as their mirrored tests run it; ADR-0089 decision 2) |
| Surl | commit `4c5010959a6096d5ee8bfcdfbef890e9d6160653` |
| Platform | Windows (`win-x64`); off Windows every case is Inconclusive until a port pin exists there (ADR-0089 decision 8) |
| Date | 2026-11-05 |

## Result

73 cases on Windows: 68 agreements, 5 port defects (one cause, below), no Surl defect, none not
comparable. Six cases are excluded on Windows by the `[OSCondition]` their mirrored tests carry (the
Linux and macOS `KEXINIT` lists, the two elliptic-curve host keys on OpenSSL builds, the account's own
`known_hosts`, and the two macOS cipher crashes).

Compared, per ADR-0089 decision 4 and its amendments: exit code, standard output, the `curl: (N)`
standard error line, the files `surl` stored for each upload case, and `surl`'s `*` notes with each
run's throwaway host-key fingerprint replaced by `<sha256>` and `<md5>`. No request bytes: the SSH
transport is encrypted. `HostKey_OtherSha256Pinned_Exits60` compares no standard error, since curl's
refusal names that run's own fingerprint; upstream's line is still checked against the mirrored
assertion. The three local-file cases (`-o` with `-C -`, `-R`, and `-Q "mtime …"` with `-R`) give each
run its own output file and compare its bytes and modification time as stored files.

Not compared (ADR-0089's BL-582 amendment): the notes `surl` writes as the client tears the connection
down - `SSH disconnect received`, `SSH channel N ended`, `SFTP session ended`, `Exchange N ended`, and
the SCP download's final acknowledgement note. Their order is the client SSH layer's (the port closes
the channel before it disconnects; libssh2 disconnects alone) and races the client's exit.

The `KEXINIT` cases run each client against its own `ClientKexInitRecorder` rather than `surl`, and
compare the identification line and the ten name-lists it records, beside the exit code and output
(both clients exit 2 when the recorder hangs up).

Intermittent: about one SSH connection in 256 from the reference build fails with exit 2,
`-8, Unable to exchange encryption keys`. That is libssh2 1.11.1 on WinCNG's own defect (ADR-0062), not
Surl's and not the port's; a re-run passes. It showed in several cases across this measurement's runs, so a
case where either run fails exactly that way is measured again, both runs, up to three attempts
(ADR-0089's BL-582 amendment).

## Findings

The test reads the first column of this table (ADR-0089 decision 5): a listed case must still show its
port defect, and an unlisted case must agree. A data-driven case is named `Method[data row]`.

| Phase 7 test | Mirrors | Finding |
| --- | --- | --- |
| `KexInit_OpenSslWindowsBuild_ListsTheOpenSslAlgorithms` | `UpstreamCurlOffersSshAlgorithmsTests.KexInit_OpenSslWindowsBuild_ListsTheOpenSslAlgorithms` | [SSH-1](#ssh-1-the-port-offers-only-the-wincng-backends-ssh-algorithms) |
| `Download_CipherOnlySurlOffers_AgreesItAndWritesTheFile[blowfish-cbc]` | `UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests.Download_CipherOnlySurlOffers_AgreesItAndWritesTheFile` | [SSH-1](#ssh-1-the-port-offers-only-the-wincng-backends-ssh-algorithms) |
| `Download_CipherOnlySurlOffers_AgreesItAndWritesTheFile[cast128-cbc]` | `UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests.Download_CipherOnlySurlOffers_AgreesItAndWritesTheFile` | [SSH-1](#ssh-1-the-port-offers-only-the-wincng-backends-ssh-algorithms) |
| `Download_MacOnlySurlOffers_AgreesItAndWritesTheFile[hmac-ripemd160]` | `UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests.Download_MacOnlySurlOffers_AgreesItAndWritesTheFile` | [SSH-1](#ssh-1-the-port-offers-only-the-wincng-backends-ssh-algorithms) |
| `Download_MacOnlySurlOffers_AgreesItAndWritesTheFile[hmac-ripemd160@openssh.com]` | `UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests.Download_MacOnlySurlOffers_AgreesItAndWritesTheFile` | [SSH-1](#ssh-1-the-port-offers-only-the-wincng-backends-ssh-algorithms) |

### SSH-1: the port offers only the WinCNG backend's SSH algorithms

- **Phase 7 tests:** the five rows above. Paired upstream build: the OpenSSL static-curl build
  (`589C8E4D…`), as each mirrored test runs it.
- **Command lines:**
  - `curl -sS -k sftp://127.0.0.1:<port>/x` against a `ClientKexInitRecorder` (the `KEXINIT` case);
  - `curl -sS --hostpubsha256 <surl's ssh-rsa SHA-256> -u tester:secret sftp://127.0.0.1:<port>/a.txt`
    against `surl sftp://… --throwaway-hostkey --user tester:secret --allow-weak-ssh-algorithms`, with
    `--ssh-ciphers blowfish-cbc` or `--ssh-ciphers cast128-cbc` (curl run with `OPENSSL_CONF` activating
    OpenSSL's `legacy` provider), or `--ssh-ciphers aes128-ctr --ssh-macs hmac-ripemd160` or
    `… hmac-ripemd160@openssh.com`.
- **Upstream:** the `KEXINIT` offers, in order, kex
  `curve25519-sha256,curve25519-sha256@libssh.org,ecdh-sha2-nistp256,ecdh-sha2-nistp384,ecdh-sha2-nistp521,`
  then the Diffie-Hellman list; host keys `ecdsa-sha2-nistp256,…,ssh-ed25519,ssh-ed25519-cert-v01@openssh.com,`
  then the RSA list; ciphers adding `aes256-gcm@openssh.com,aes128-gcm@openssh.com`, `blowfish-cbc` and
  `cast128-cbc`; MACs adding `hmac-ripemd160,hmac-ripemd160@openssh.com`. Each weak-algorithm download
  exits 0 with `hello world\n`, and `surl` notes `SSH negotiated … cipher blowfish-cbc/blowfish-cbc …`
  (or the cipher or MAC named).
- **Port:** the `KEXINIT` differs from line 2 (the kex name-list): it offers exactly the Windows
  reference build's WinCNG lists (`diffie-hellman-group-exchange-sha256,…,kex-strict-c-v00@openssh.com`;
  RSA host keys only; no GCM, Blowfish, CAST-128 or RIPEMD-160). Each weak-algorithm download exits 2,
  `curl: (2) Failure establishing ssh session: -5, Unable to exchange encryption keys`, no output, and
  `surl` notes `SSH no common cipher algorithm; client offered chacha20-poly1305@openssh.com,aes256-ctr,…`
  (or `no common MAC algorithm`).
- **Agreement beside it:** `KexInit_WindowsBuild_ListsTheWinCngAlgorithms` agrees: the port's lists are
  the reference build's, identification `SSH-2.0-libssh2_1.11.1` included.
- **What is right:** upstream curl 8.21.0 with libssh2 1.11.1 offers the algorithms of the crypto
  backend libssh2 was built with (libssh2 1.11.1 `src/crypt.c`, `src/mac.c`, `src/kex.c` and
  `src/hostkey.c`, whose OpenSSL entries are compiled in under `LIBSSH2_OPENSSL`); RFC 4253 section 6.3
  names `blowfish-cbc` and `cast128-cbc` as OPTIONAL ciphers, RFC 5656 the ECDH and ECDSA names, RFC
  8731 `curve25519-sha256` and RFC 8709 `ssh-ed25519`. A client that is upstream curl's port on an
  OpenSSL-backed build offers those; the port offers the WinCNG backend's lists whatever it runs on.
  Whether the port should follow the OpenSSL builds or keep the reference build's lists is the port
  maintainer's call; against the OpenSSL pairing these cases use, it differs.
