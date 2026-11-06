# ADR-0147 — The remaining TLS 1.3 groups curl offers over QUIC, and the libraries they need

- **Status:** Accepted
- **Date:** 2026-11-06
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-06, in BL-545.
  Measured and written in BL-545, recorded by BL-1005 after BL-545 was split.
- **Acted on by:** BL-1006 (ML-KEM-1024), BL-1007 (`Surl.Cryptography.Curve448`), BL-1029 (`Surl.Cryptography.Sm2`), and BL-1008, BL-1009, BL-1010 and BL-1030 (`Surl.Tls13`).
- **Builds on:** [ADR-0082](ADR-0082-how-surl-serves-http-3-over-quic.md) decision 3
  (the server's group order; "the groups curl lists only in `supported_groups` ... are served too, by BL-545")
  and decision 2 (what is hand-built); [ADR-0002](ADR-0002-mirror-the-curl-ports-project-map.md) decision 3 (the table of
  horizontal libraries, which this ADR amends).
- **Corrects:** ADR-0082's table of what curl sends, which names `11EE` SecP384r1MLKEM1024. `11EE` is
  curveSM2MLKEM768; SecP384r1MLKEM1024 is `11ED` (IANA TLS Supported Groups registry, and measured below).

## Context

ADR-0082 decision 3 serves X25519MLKEM768, x25519, secp256r1, secp384r1 and secp521r1. The pinned OpenSSL
builds list six more groups in `supported_groups` and send a share for none of them; a curl offers one of
them on its own only when `--curves` names it alone. BL-545 measured every pinned HTTP/3 build with
`--curves <group> --http3-only` on 2026-11-06 (`Record-CurlExchange.ps1 -QuicInitial` on Windows; the Linux
build in WSL against `socat`, decrypted by `Run-QuicInitialRecorder.cs`, as BL-375 did):

| `--curves` | static 8.21.0 OpenSSL 4.0.1, Windows and Linux: `supported_groups` and key share | curl.se 8.22.0_2 LibreSSL 4.3.2, Windows |
| --- | --- | --- |
| `SecP256r1MLKEM768` | `11EB` alone; 1249 bytes: a P-256 point (65), then an ML-KEM-768 encapsulation key (1184) | refused: exit 59, "Could not use specified SSL cipher" |
| `SecP384r1MLKEM1024` | `11ED` alone; 1665 bytes: a P-384 point (97), then an ML-KEM-1024 encapsulation key (1568) | refused, exit 59 |
| `curveSM2MLKEM768` | `11EE` alone; 1249 bytes: a curveSM2 point (65), then an ML-KEM-768 encapsulation key | refused, exit 59 |
| `X448` | `001E` alone; 56 bytes | refused, exit 59 |
| `curveSM2` | `0029` alone; 65 bytes, an uncompressed point | refused, exit 59 |
| `ffdhe2048` | `0100` alone; 256 bytes | refused, exit 59 |
| `ffdhe3072` | `0101` alone; 384 bytes | refused, exit 59 |

The macOS build is the same source and OpenSSL as the Windows and Linux static builds and is not on the
measuring machine; CI's macOS leg runs the same unit tests.

## Decision

1. **Every group in the table is served**, after ADR-0082 decision 3's five, in this order:
   SecP256r1MLKEM768, SecP384r1MLKEM1024, curveSM2MLKEM768, x448, curveSM2, ffdhe2048, ffdhe3072. The order
   matters only to a client that offers several of them without one of the first five, which no pinned build
   does; the hybrids come first because they are the stronger choice, then curl's own `supported_groups` order.
   A ClientHello listing one of them with no share for it gets a HelloRetryRequest naming it, as for the first
   five.
2. **The shares.** SecP256r1MLKEM768 and SecP384r1MLKEM1024 are draft-ietf-tls-ecdhe-mlkem: the client sends
   the uncompressed NIST point then the ML-KEM encapsulation key, the server answers its point then the
   ciphertext, and the shared secret is the ECDH secret then ML-KEM's. curveSM2MLKEM768
   (draft-yang-tls-hybrid-sm2-mlkem) is laid out the same way with curveSM2 in the NIST curve's place, as the
   recorded share shows (the 0x04 point first). x448 is RFC 7748 with RFC 8446 section 7.4.2's all-zero check.
   curveSM2 is RFC 8998, with RFC 8446 sections 4.2.8.2 and 7.4.2: an uncompressed point, the secret the product's x-coordinate. ffdhe2048
   and ffdhe3072 are RFC 7919 with RFC 8446 sections 4.2.8.1 and 7.4.1: Y and the secret padded to the length
   of p, and 1 &lt; Y &lt; p - 1 checked; the server's exponent is 256 bits for ffdhe2048 and 320 for ffdhe3072,
   above RFC 7919 section 5.2's 225 and 275. Anything else is `illegal_parameter`, as ADR-0082 decision 3 says
   for a malformed share. Randomness keeps coming from the injected `RandomNumberGenerator`: for a hybrid
   ML-KEM's m first, then the classical private key.
3. **What is hand-built, and where.** Two new libraries, each in `Cryptography/` with its own `.UnitTests`
   project and the root `CLAUDE.md` quality gates, and ML-KEM-1024 beside ML-KEM-768:

   | Library | Folder | Holds | May itself reference |
   | --- | --- | --- | --- |
   | `Surl.Cryptography.Curve448.UnitLibrary` | `Cryptography/` | X448 (RFC 7748). Hand-built: the BCL has no X448 on any platform. | nothing |
   | `Surl.Cryptography.Sm2.UnitLibrary` | `Cryptography/` | Elliptic-curve Diffie-Hellman on the SM2 recommended curve (GB/T 32918.5, RFC 8998's curveSM2). Hand-built: the BCL has no SM2 curve, and an explicit curve is not available on macOS. | nothing |

   `Surl.Cryptography.MlKem.UnitLibrary` gains ML-KEM-1024 (FIPS 203) beside ML-KEM-768, sharing K-PKE through
   a parameter set. `Surl.Tls13.UnitLibrary` may now reference `Surl.Cryptography.Curve25519.UnitLibrary`,
   `Surl.Cryptography.Curve448.UnitLibrary`, `Surl.Cryptography.MlKem.UnitLibrary` and
   `Surl.Cryptography.Sm2.UnitLibrary`. The NIST curves inside the hybrids stay the BCL's `ECDiffieHellman`, and
   ffdhe is `System.Numerics.BigInteger.ModPow` inside `Surl.Tls13` (`TlsFiniteFieldGroup`): neither is a new
   primitive.

## Consequences

- A curl told `--curves` with any one of these groups now completes the TLS handshake and so reaches HTTP/3,
  where before it was answered `handshake_failure`.
- The LibreSSL build refuses each of these names itself, so nothing changes for it.
- `ProtocolIsolationTests` carries the two new rows and `Surl.Tls13`'s wider reference list.
