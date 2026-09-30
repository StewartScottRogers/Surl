# Surl.Cryptography.Ripemd160.UnitLibrary

Empty until BL-257 fills it.

Will hold RIPEMD-160 (Dobbertin, Bosselaers and Preneel, 1996) and HMAC-RIPEMD-160
(RFC 2286). The base class library has no RIPEMD-160, and its `HMAC` cannot take a hash
it does not know, so both are hand-built here (ADR-0061). `hmac-ripemd160` and
`hmac-ripemd160@openssh.com` are composed from it in `Surl.Protocol.Ssh`, not here.

It references nothing (ADR-0061); protocol servers may reference it as a horizontal
library. Bytes in, bytes out: never open a socket or a file, and never read a clock.

Every behaviour will be tested against published vectors, with the source cited beside
each test: the RIPEMD-160 authors' published vectors for the hash, and RFC 2286 section 2
for the HMAC. Copying RIPEMD-160 and its tests from the Curl port is allowed: code is not
a verdict, and no expected value comes from it (ADR-0003).
