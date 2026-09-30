# Surl.Cryptography.Blowfish.UnitLibrary

Empty until BL-255 fills it.

Will hold Blowfish (Schneier, 1993): the standard key schedule, the salted expansion
bcrypt's eksblowfish needs, and one 8-byte block encrypted or decrypted. The base class
library has no Blowfish, so it is hand-built (ADR-0061). BL-255 moves the Blowfish now
internal to `Surl.Cryptography.BcryptPbkdf.UnitLibrary` here and makes it public;
`Surl.Cryptography.BcryptPbkdf.UnitLibrary` then references this library. `blowfish-cbc`
(RFC 4253 section 6.3) is composed from it in `Surl.Protocol.Ssh`, not here.

It references nothing (ADR-0061); protocol servers may reference it as a horizontal
library. Bytes in, bytes out: never open a socket or a file, and never read a clock.

Every behaviour will be tested against published vectors, with the source cited beside
each test: Eric Young's ECB vectors in Schneier's `vectors-2.txt`. Copying Blowfish and its
tests from the Curl port is allowed: code is not a verdict, and no expected value comes
from it (ADR-0003).
