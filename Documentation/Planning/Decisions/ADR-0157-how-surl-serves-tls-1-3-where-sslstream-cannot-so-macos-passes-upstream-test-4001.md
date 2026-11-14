# ADR-0157 — How Surl serves TLS 1.3 where `SslStream` cannot, so macOS passes upstream test 4001

- **Status:** Accepted
- **Date:** 2026-11-14
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-14,
  in BL-1144 (FR-053). Built by BL-1145, BL-1146 and BL-1147.
- **Amends:** [ADR-0010](ADR-0010-the-server-side-tls-contract.md) section 1 (which stream secures
  a connection) for one case: a ClientHello that only a TLS 1.3 server can answer, on a platform
  whose `SslStream` has no TLS 1.3.

## Context

BL-1141 found, in CI run 43637240745, that upstream test 4001 (`--ech ecl:... -k`; the server ignores
ECH) ends with curl exit 35 on `osx-arm64` instead of 101 (`CURLE_ECH_REQUIRED`). ECH needs TLS 1.3,
so curl offers 1.3 and nothing lower. .NET's `SslStream` server on macOS uses Apple Secure
Transport, which negotiates up to TLS 1.2, so the handshake fails with a protocol-version alert
(`Authentication failed` in the server half's log). Every other macOS case passes because curl
offers 1.2 and the handshake settles there. Linux (OpenSSL) passes. This is the platform primitive's
limit, not a defect in the test-case half.

CLAUDE.md's standing rule is that nothing is left out because the BCL has no primitive. `Surl.Tls13`
already holds the server half of the TLS 1.3 handshake (ADR-0082 decision 2; ClientHello parsing,
key schedule, transcript, EncryptedExtensions, Certificate, CertificateVerify, Finished, the groups
of ADR-0147) for QUIC, which has no record layer. What is missing is TLS 1.3's own record layer and a
`Stream` that drives the handshake over TCP.

## Decision

1. **Hand-build a TLS 1.3 server stream and use it only where `SslStream` cannot serve the client.**
   Not on every platform: `SslStream` stays the answer wherever it works, so the behaviour every
   passing case already depends on does not move. The hand-built stream is chosen when both hold:
   the process's `SslStream` has no TLS 1.3 server (today: macOS, detected once through
   `OperatingSystem.IsMacOS()` in `Surl.Networking`, kept in a small injectable
   `ITls13Support` so tests set it), **and** the ClientHello's `supported_versions` lists TLS 1.3
   and nothing lower. A client that also offers 1.2 still gets `SslStream`, as today.

2. **Three pieces, each in a project held to the quality gates:**
   - `Surl.Tls13.UnitLibrary` gains the record layer (RFC 8446 section 5): `TLSCiphertext`
     framing, per-direction sequence-number nonces, AEAD through the BCL's `AesGcm` for
     `TLS_AES_128_GCM_SHA256` and `TLS_AES_256_GCM_SHA384`, and `Surl.Cryptography.ChaCha20` with
     `Surl.Cryptography.Poly1305` for `TLS_CHACHA20_POLY1305_SHA256` (the suites the handshake
     already negotiates), the inner content type and padding, and the 2^14 plaintext limit.
   - A new `Surl.Tls13Stream.UnitLibrary` with `.UnitTests` (in `Cryptography/`, beside
     `Surl.TlsSrp`, whose `TlsSrpStream` it mirrors) holds `Tls13ServerStream`: reads the
     ClientHello record, runs `TlsServerHandshake`, writes the server flight, checks the client
     Finished, then exposes the application data as a `Stream`, answering `close_notify`,
     refusing a non-alert plaintext record after the handshake, and treating a middlebox
     `change_cipher_spec` (RFC 8446 appendix D.4) as ignorable. A fatal condition writes the alert
     and fails with `TlsHandshakeException`-compatible information for `Surl.Networking` to wrap.
   - `Surl.Networking.UnitLibrary`'s `ServerTlsHandshake` reads the ClientHello, applies decision 1
     and returns either the `SslStream` or the `Tls13ServerStream` (replaying the buffered
     ClientHello bytes to the chosen one), with the negotiated `TlsSession` filled in the same way.
     Only `Surl.Networking` references the new library, as it already does `Surl.TlsSrp`.

3. **Scope of the stream:** server only, no client certificates (ADR-0010 section 5's request goes
   through `SslStream`, so a client certificate setting makes decision 1's predicate false and the
   error stays as today), no session tickets (as `TlsServerHandshake`), no 0-RTT, `KeyUpdate`
   answered when received. ALPN comes from the same listener offer list. The certificate and key are
   the ones `ServerTlsSettings` already holds, passed as `TlsServerHandshakeOptions` does.

4. **Evidence.** The record layer is checked against RFC 8448's traces (which include protected
   records); the stream by unit tests over an in-memory duplex pair; the end result by upstream
   test 4001 returning exit 101 on `osx-arm64` in CI, recorded by BL-1142. No expected byte comes
   from the Curl port.

5. **Ordered implementation tasks:** BL-1145 (record layer), BL-1146 (`Surl.Tls13Stream`, depends
   on BL-1145), BL-1147 (`Surl.Networking` selection, depends on BL-1146). BL-1142, which records
   the pass rates, depends on BL-1147.

## Consequences

- macOS serves TLS 1.3-only clients (ECH, `--tlsv1.3`) the same way Linux does, closing a class of
  platform gaps, not only 4001.
- Surl now holds a second, hand-built TLS server path in production; it is exercised by a real
  platform only on macOS, so its unit tests carry the weight on Windows and Linux.
- Choosing it by ClientHello keeps the blast radius to clients `SslStream` could not serve anyway.
