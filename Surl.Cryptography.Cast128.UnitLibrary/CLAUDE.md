# Surl.Cryptography.Cast128.UnitLibrary

Empty until BL-256 fills it.

Will hold CAST-128 (RFC 2144) with 40- to 128-bit keys, and one 8-byte block encrypted or
decrypted. The base class library has no CAST-128, so it is hand-built (ADR-0061).
`cast128-cbc` (RFC 4253 section 6.3) is composed from it in `Surl.Protocol.Ssh`, not here.

It references nothing (ADR-0061); protocol servers may reference it as a horizontal
library. Bytes in, bytes out: never open a socket or a file, and never read a clock.

Every behaviour will be tested against published vectors, with the source cited beside
each test: RFC 2144 Appendix B. Copying CAST-128 and its tests from the Curl port is
allowed: code is not a verdict, and no expected value comes from it (ADR-0003).
