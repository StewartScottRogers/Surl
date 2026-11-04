# Surl.Cryptography.Rc4.UnitLibrary

Holds `Rc4`: the RC4 stream cipher, set up from a key of 1 to 256 bytes (anything else
throws `ArgumentException`) and a count of keystream bytes to discard, whose
`ApplyKeyStream` exclusive-ors the next keystream bytes into a span and keeps its state
across calls, because an SSH connection runs one keystream across every packet. It is what
`arcfour` (RFC 4253 section 6.3: 128-bit key, nothing discarded) and `arcfour128` and
`arcfour256` (RFC 4345 section 4: the first 1536 bytes discarded) are built from; composing
those ciphers is `Surl.Protocol.Ssh`'s work, not this library's. The base class library has
no RC4, so it is hand-built (ADR-0051).

It references nothing (ADR-0051); protocol servers may reference it as a horizontal
library. Bytes in, bytes out: never open a socket or a file, and never read a clock.

Every behaviour is tested against published vectors, with the source cited beside each
test: RFC 6229 section 2's keystream for the 40-, 128- and 256-bit keys, and its offset-1536
row for RFC 4345's discard. Copying RC4 and its tests from the Curl port is allowed: code
is not a verdict, and no expected value comes from it (ADR-0003).
