# Surl.Cryptography.Curve25519.UnitLibrary

Phase 2, when the SSH server needs it.

Will hold GF(2^255-19) field arithmetic and X25519 key agreement (RFC 7748), hand-built
because the base class library has no X25519 on any platform (ADR-0048). Holds nothing
yet; BL-150 builds it.

It references nothing (ADR-0048). `Surl.Cryptography.Ed25519.UnitLibrary` references it
for the field arithmetic, and protocol servers may reference it as a horizontal library.
Bytes in, bytes out: never open a socket or a file, and never read a clock.

Every function is tested against RFC 7748's published vectors (section 5.2 and section
6.1), with the source cited beside each vector. Copying the field arithmetic and X25519
and their tests from the Curl port is allowed: code is not a verdict, and no expected
value comes from it (ADR-0003).
