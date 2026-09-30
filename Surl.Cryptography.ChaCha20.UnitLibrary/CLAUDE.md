# Surl.Cryptography.ChaCha20.UnitLibrary

Holds `ChaCha20`: the ChaCha20 block function and stream cipher in two forms that share
one block function (ADR-0048). `ComputeBlock` and `ApplyKeyStream` are RFC 8439's 96-bit
nonce and 32-bit counter (sections 2.1 to 2.4); `ComputeOriginalBlock` and
`ApplyOriginalKeyStream` are the 64-bit nonce and 64-bit block counter
`chacha20-poly1305@openssh.com` uses (OpenSSH `PROTOCOL.chacha20poly1305`). The counter
never wraps: a call whose last block would pass the form's largest counter throws
`ArgumentOutOfRangeException` before writing anything. The base class
library's `ChaCha20Poly1305` is RFC 8439's AEAD only, with no raw keystream, so it cannot
stand in. Composing `chacha20-poly1305@openssh.com` from this and Poly1305 is
`Surl.Protocol.Ssh`'s work, not this library's.

It references nothing (ADR-0048); protocol servers may reference it as a horizontal
library. Bytes in, bytes out: never open a socket or a file, and never read a clock.

Every function is tested against published vectors, with the source cited beside each:
RFC 8439 sections 2.1.1, 2.2.1, 2.3.2, 2.4.2 and appendix A.1 and A.2 for the RFC form,
and draft-strombergson-chacha-test-vectors-01 (TC7 and TC8, 256-bit key, 20 rounds) for
the original form. Copying
ChaCha20 and its tests from the Curl port is allowed: code is not a verdict, and no
expected value comes from it (ADR-0003).
