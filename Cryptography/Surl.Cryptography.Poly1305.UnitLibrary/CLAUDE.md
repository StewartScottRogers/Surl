# Surl.Cryptography.Poly1305.UnitLibrary

Holds `Poly1305`, the Poly1305 one-time authenticator of RFC 8439 section 2.5, for the SSH
server's `chacha20-poly1305@openssh.com`, hand-built because the base class library has no
stand-alone Poly1305 (ADR-0048). `Poly1305.ComputeTag(key, message)` returns the 16-byte
tag of a whole message under a 32-byte one-time key; an overload writes it into a
caller's span. One-shot only: SSH authenticates the encrypted length and the ciphertext as
one message. Checking a received tag is the caller's job, with
`CryptographicOperations.FixedTimeEquals`.

It references nothing (ADR-0048); protocol servers may reference it as a horizontal
library. Bytes in, bytes out: never open a socket or a file, and never read a clock.
Constant-time: no branch or index depends on the key or the accumulator.

Every function is tested against RFC 8439's published vectors (section 2.5.2 and appendix
A.3 #1 to #11), with the source cited beside each vector. The code was copied from the
Curl port: code is not a verdict, and no expected value comes from it (ADR-0003).
