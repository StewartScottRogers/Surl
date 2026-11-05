---
id: BL-563
title: Decide how Surl serves HTTPS over TLS-SRP
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-04
completed:
---
# BL-563 — Decide how Surl serves HTTPS over TLS-SRP

## Goal

An ADR decides how Surl serves HTTPS over TLS-SRP (RFC 5054) for upstream curl's
`--tlsauthtype SRP --tlsuser <user> --tlspassword <password>`: which TLS version and cipher suites,
the hand-built libraries that do what `SslStream` cannot, where SRP verifiers come from, and how
`surl` is asked for it.

## Context

- ADR-0080 decision 2 marks `httptls+srp` and `httptls+srp-ipv6` `server-not-built`. Upstream test
  cases that wait on this (tag `curl-8_21_0`, commit `68720b4837284335b2d63cb358f8f6ce65f5bc55`,
  classified with ADR-0080's rules by BL-399): `httptls+srp` 320, 321, 322, 324 (feature
  `TLS-SRP`; `--tlsauthtype SRP --tlsuser jsmith --tlspassword abc`); `httptls+srp-ipv6` has no
  case at the tag.
- Which pins can run them: ADR-0016 says the Linux and macOS reference pins report `TLS-SRP`; the
  Windows reference pin is Schannel and does not, so there the cases skip
  `feature-missing:TLS-SRP`.
- `SslStream` offers no SRP cipher suite, so this is hand-built (root `CLAUDE.md`, "Decisions": each
  hand-built piece in its own `Surl.<Area>.UnitLibrary` with its `.UnitTests` twin, held to the
  quality gates; a hand-built primitive goes in `Cryptography/`). BCL pieces available:
  `System.Numerics.BigInteger.ModPow`, `SHA1`, `SHA256`, `HMACSHA1`, `HMACSHA256`, `Aes` (CBC),
  `TripleDES`, `RandomNumberGenerator`.
- Specifications: RFC 5054 (SRP for TLS: the `srp` extension, ServerKeyExchange, ClientKeyExchange,
  the groups of Appendix A, the test vectors of Appendix B, the cipher suites of section 2.7), RFC
  2945 and SRP-6a, RFC 5246 (TLS 1.2 record layer, PRF, Finished), and any extension curl's OpenSSL
  ClientHello carries that the server must answer (RFC 5746 renegotiation info, RFC 7627 extended
  master secret, RFC 7366 encrypt-then-MAC). Measure the ClientHello and every following byte of the
  pinned Linux and macOS 8.21.0 builds with `Record-CurlExchange.ps1`; never the Curl port
  (ADR-0003). https://curl.se/docs/manpage.html (`--tlsauthtype`, `--tlsuser`, `--tlspassword`,
  `--proxy-tlsauthtype`).
- Existing hand-built TLS work to compare with: `Surl.Tls13` (BL-375, TLS 1.3 for QUIC) and
  `Surl.Dtls` (BL-412, DTLS 1.2 record layer). The ADR says whether TLS 1.2 key derivation or record
  code is shared with either, or kept apart.

Decide and record:

1. The libraries. Planning assumption: `Cryptography/Surl.Cryptography.Srp.UnitLibrary` for the
   SRP-6a math (verifier, `k`, `u`, `B`, the premaster secret, group parameters), and
   `Cryptography/Surl.TlsSrp.UnitLibrary` for the TLS 1.2 record layer and the server handshake;
   each with its `.UnitTests` twin. Amend ADR-0002 decision 3's reference table.
2. The TLS version and cipher suites served: the ones curl 8.21.0's OpenSSL offers with
   `--tlsauthtype SRP`, measured, and which of RFC 5054's nine are implemented (the RSA- and
   DSS-signed ones need a certificate signature).
3. The extensions answered, and what the server does with each one it does not implement.
4. Verifiers: the file format the server reads (planning assumption: GnuTLS's `tpasswd` and
   `tpasswd.conf` format, which upstream's test server reads; the ADR checks whether
   `tests/certs` at the tag carries a verifier file and whether ADR-0080's import includes it), or a
   plain user and password from which the server computes the verifier, and the unknown-user answer
   (RFC 5054 section 2.5.1.3).
5. How the TLS-SRP connection plugs into the listener seam so `Surl.Protocol.Http` runs over it
   unchanged, and which code constructs nothing it must not (root `CLAUDE.md`).
6. The command line (planning assumption: `surl https://127.0.0.1:8443 --tlsauthtype SRP
   --tlsuser jsmith --tlspassword abc`, mirroring curl's option names, or `--srp-verifier <file>`),
   each failure's `SurlExitCode`, and the alerts sent for each handshake failure.
7. How the upstream test cases' server half plays `httptls+srp` and `httptls+srp-ipv6`.

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked
      "Decided by Claude under Stewart's delegation", answering decisions 1 to 7, each citing its
      RFC section or the curl 8.21.0 measurement it rests on (with the pinned build's file and
      SHA-256 from `UpstreamCurlBuilds.json`), and it is listed in the folder's `README.md`.
- [ ] The ADR names cases 320, 321, 322 and 324, states that they run on the Linux and macOS
      reference pins only, and names the tasks that act on it: BL-564, BL-565, BL-566, BL-567 and
      BL-568.

## Notes

- Planning assumptions above are BL-399's defaults for an unattended plan; the ADR may change any
  of them, and the build tasks' `touches` follow the ADR (a task records the change in its Notes).
- Measuring needs the Linux or macOS reference pin; if neither is available on the machine running
  this task, the measurement runs in CI's Linux leg, and the ADR says which.

## Log

- 2026-11-04: Created.
