# Phase 7 findiRun [44044472038](https://github.com/StewartScottRogers/Surl/actions/runs/44044472038) of `curl-port-phase-7` (2026-11-17, `factory/phase-1` at Surl commit `a5337bdf7e6d54fa3337c51008d5c66b814355e2`, BL-1339) is the current linux-x64 outcome and supersedes the run below. Nothing threw `UnpinnedCurlPortClientException`: the test job accepted the hash its own build job reproduced (BL-1340, BL-1339), so cases compared pinned upstream curl with the Curl port. This family: 67 passed, 5 failed, 7 not executed. A failing case is a real difference not yet listed in a family findings table; each is follow-up work, not a CI finding.

ngs: SCP and SFTP

What `CurlPortBesideUpstreamCurlOverSshTests` in `Conformance/Surl.Conformance.UnitTests` found when it
ran every SCP and SFTP conversation of `UpstreamCurlTransfersFilesWithSurlOverScpTests`,
`UpstreamCurlTransfersFilesWithSurlOverSftpTests`, `UpstreamCurlLogsInToSurlOverSshTests`,
`UpstreamCurlOffersSshAlgorithmsTests` and `UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests` through
pinned upstream curl and the pinned Curl port, each against its own fresh `surl` and with its own empty
`HOME` and `USERPROFILE` (ADR-0089, ADR-0051 decision 8, BL-582).

## Measured

| What | Pin |
| --- | --- |
| Curl port | commit `74f3f45c47e86300b2b262dc1dacd43b508b09aa`, `curl.exe` SHA-256 `7F4ED0559C543A1E2C415C8EC2C4C458F1906A54910907D3546479A42ECFE467` (`CurlPortBuilds.json`, `win-x64`) |
| Upstream curl, reference | SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 libidn2/2.3.8 libpsl/0.21.5 libssh2/1.11.1 WinLDAP` (the Windows reference build; every case but those below) |
| Upstream curl, OpenSSL | SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`, `curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 OpenSSL/4.0.1 zlib/1.3.2 brotli/1.2.0 zstd/1.5.7 c-ares/1.34.6 libidn2/2.3.8 libpsl/0.22.0 libssh2/1.11.1 nghttp2/1.69.0 ngtcp2/1.23.0 nghttp3/1.16.0` (the supplementary static-curl build of ADR-0063, paired with `KexInit_OpenSslWindowsBuild_ListsTheOpenSslAlgorithms` and every `UpstreamCurlAgreesWeakSshAlgorithmsWithSurlTests` case, as their mirrored tests run it; ADR-0089 decision 2) |
| Surl | commit `4c5010959a6096d5ee8bfcdfbef890e9d6160653` |
| Platform | Windows (`win-x64`); off Windows every case is Inconclusive until a port pin exists there (ADR-0089 decision 8) |
| Date | 2026-11-05; re-run 2026-11-16 at port commit 74f3f45: every listed finding still holds, none fixed, none added |

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

## osx-arm64

Run [44000208980](https://github.com/StewartScottRogers/Surl/actions/runs/44000208980) of `curl-port-phase-7`
(2026-11-17, `factory/phase-1` at Surl commit `c87b86d5c2ed723aeaf615bbc980b8d31d44b462`, Curl port commit
`74f3f45c47e86300b2b262dc1dacd43b508b09aa`) completed with the `Phase 7 families (osx-arm64)` job green
(BL-1324). Its `phase-7-osx-arm64-results` artifact holds 489 results for all Phase 7 test classes: 1 passed, 131 not
executed, and every case of this family Inconclusive or failed before any comparison.

Findings:

- **Nothing was compared.** `UpstreamCurlBuilds.json` pins no upstream curl build for `osx-arm64`, so the mirrored
  tests are Inconclusive there with "pins no upstream curl build for osx-arm64" (upstream curl is the only oracle,
  ADR-0003). Pinning one means downloading a build, which needs Stewart's approval (CLAUDE.md, "Decisions").
- **The osx-arm64 port build is not pinned by hash on a CI runner.** The runner's published `curl` hashed
  `71F9BB5EED0DCAB034D33883365082D2BA19758E9549B140037C4E663A98A0D3`, not the pinned
  `B5B48DC7CB11F42F41BD7F45E127ADF08E6BD1A0D3CAA0F80840E1971046BBEE` (`ciHashStable: false`), so cases that reach the port
  throw `UnpinnedCurlPortClientException`. The hash changes with each publish; it is not a port defect.
- **No port defect and no Surl defect is recorded for osx-arm64.** The family's findings table is unchanged.

## linux-x64

Run [44012635963](https://github.com/StewartScottRogers/Surl/actions/runs/44012635963) of `curl-port-phase-7`
(2026-11-17, `factory/phase-1` at Surl commit `a6a289ce2e2d9691c1dd46894f92002bd84ce249`, Curl port commit
`74f3f45c47e86300b2b262dc1dacd43b508b09aa`) completed; its `Phase 7 families and OpenLdap_ cases (linux-x64)` job
ran green because the test step is `continue-on-error` (BL-1307). Its `phase-7-linux-x64-results` artifact holds 489
results for all Phase 7 test classes: 1 passed, 441 failed, 47 not executed.

Findings:

- **Nothing was compared.** All 441 failures are the same `UnpinnedCurlPortClientException`: the port published in the
  test job hashed `048AAEE01EAD37D136AF842303E8C90438B1E193DEDC0654F3CFCA4A1E555364`, but `CurlPortBuilds.json` pins
  `DF507F32E8B3E5F0444F89544A62C18C4CFA96C838AF8C98D74B9B044F8CF57A`, which the `Build the Curl port (linux-x64)` job of
  the same run reproduced twice. The port was published before any case ran, so the difference comes from the test
  job, not from the port. It is a CI finding, not a port defect and not a Surl defect.
- **Not executed:** 23 Windows-only, 2 macOS-only, and 15 `Assert.Inconclusive` ("pins no supplementary upstream curl
  build for linux-x64") cases, plus the Kerberos/GSSAPI cases.
- **No port defect and no Surl defect is recorded for linux-x64.** The family's findings table is unchanged.
