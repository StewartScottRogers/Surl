# Surl.Cryptography.Poly1305.UnitLibrary

Phase 2, when the SSH server needs it.

Will hold the Poly1305 one-time authenticator (RFC 8439 section 2.5), for the SSH server's
`chacha20-poly1305@openssh.com`, hand-built because the base class library has no
stand-alone Poly1305 (ADR-0048). Holds nothing yet; its task builds it.

It references nothing (ADR-0048); protocol servers may reference it as a horizontal
library. Bytes in, bytes out: never open a socket or a file, and never read a clock.

Every function is tested against RFC 8439's published vectors (section 2.5.2 and appendix
A.3), with the source cited beside each vector. Copying Poly1305 and its tests from the
Curl port is allowed: code is not a verdict, and no expected value comes from it
(ADR-0003).
