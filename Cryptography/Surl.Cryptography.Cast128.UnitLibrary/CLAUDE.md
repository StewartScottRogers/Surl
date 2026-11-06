# Surl.Cryptography.Cast128.UnitLibrary

Holds CAST-128 (RFC 2144): `Cast128`, a block cipher keyed with 5 to 16 bytes (40 to 128
bits, zero-padded on the right; 12 rounds for keys of 10 bytes or less, 16 above), that
encrypts or decrypts one 8-byte big-endian block with `EncryptBlock` and `DecryptBlock`, and
zeroes its subkeys on `Dispose`. `Cast128SubstitutionBoxes` holds the S-boxes S1-S8 of
Appendix A. The base class library has no CAST-128, so it is hand-built (ADR-0061).
`cast128-cbc` (RFC 4253 section 6.3) is composed from it in `Surl.Protocol.Ssh`, not here:
this library has no chaining mode.

It references nothing (ADR-0061); protocol servers may reference it as a horizontal
library. Bytes in, bytes out: never open a socket or a file, and never read a clock.

Every behaviour is tested against published vectors, with the source cited beside each
test: RFC 2144 Appendix B (B.1's three keys, B.2's full maintenance test). Copying CAST-128
and its tests from the Curl port is allowed: code is not a verdict, and no expected value
comes from it (ADR-0003).
