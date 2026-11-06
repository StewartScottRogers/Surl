# Surl.Cryptography.Srp.UnitLibrary

Holds the server side of SRP-6a for TLS (RFC 5054 sections 2.4 to 2.6, RFC 2945), decided in
ADR-0122 decision 1:
- `SrpGroup` is one group (`N`, `g`, and `N`'s byte length for `PAD`); its static members are
  the seven groups of RFC 5054 Appendix A, and `IsRfc5054Group` says whether a group read from a
  file is one of them (OpenSSL's client accepts no other).
- `Srp6a` holds the formulas: `PAD`, `x = SHA1(s | SHA1(I | ":" | P))`, `v = g^x % N`,
  `k = SHA1(N | PAD(g))`, `A = g^a % N` (for tests that play the client),
  `B = (k*v + g^b) % N`, `u = SHA1(PAD(A) | PAD(B))`, the `A % N = 0` check and the server's
  premaster secret `(A * v^u)^b % N`.
- `SrpServerKeyExchange` is one handshake's server half: it draws `b` (32 bytes) from an
  injected `RandomNumberGenerator`, offers `B`, and turns the client's `A` into the premaster
  secret, refusing an `A` with `A % N = 0`.
- `SrpGroupFile` and `SrpVerifierFile` read GnuTLS's `tpasswd.conf` (`index:N:g`) and `tpasswd`
  (`user:verifier:salt:index`) text, the formats of upstream's `tests/certs/srp-verifier-conf`
  and `srp-verifier-db` (ADR-0122 decision 4), with `GnuTlsSrpBase64` decoding their numbers. A
  malformed line is an `SrpFileFormatException` carrying its line number; the host turns it into
  `CouldNotReadFile` (37) naming the file (ADR-0122 decision 6).

Numbers cross the public API as unsigned big-endian bytes or `BigInteger`. `B` and the premaster
secret come out with no leading zero byte, as OpenSSL's `BN_bn2bin` gives them.

It references nothing (ADR-0122 decision 1); `Surl.TlsSrp.UnitLibrary` builds the TLS 1.2
handshake on it. Text and bytes in, values out: never open a socket or a file, and never read a
clock. The base class library does the arithmetic (`BigInteger.ModPow`, `SHA1`).

Every formula is tested against RFC 5054 Appendix B's vectors byte for byte, and every Appendix A
group against the RFC's prime (its SHA-256) and generator. Expected values come from the RFC and
GnuTLS's documented formats only, never from the Curl port (ADR-0003).
