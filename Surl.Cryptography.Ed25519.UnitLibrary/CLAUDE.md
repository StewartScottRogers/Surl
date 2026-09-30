# Surl.Cryptography.Ed25519.UnitLibrary

Phase 2, when the SSH server needs it.

Will hold Ed25519 key generation, signing and verification (RFC 8032 section 5.1), for the
SSH server's `ssh-ed25519` host keys, hand-built because the base class library has no
Ed25519 on any platform (ADR-0048). Holds nothing yet; its task builds it. Its SHA-512
comes from `System.Security.Cryptography` and is never rebuilt here.

It references `Surl.Cryptography.Curve25519.UnitLibrary` for the field arithmetic and
nothing else (ADR-0048); protocol servers may reference it as a horizontal library.
Bytes in, bytes out: never open a socket or a file, and never read a clock.

Every function is tested against RFC 8032's published vectors (section 7.1), with the
source cited beside each vector. Copying the Edwards-curve, scalar and Ed25519 code and
their tests from the Curl port is allowed: code is not a verdict, and no expected value
comes from it (ADR-0003).
