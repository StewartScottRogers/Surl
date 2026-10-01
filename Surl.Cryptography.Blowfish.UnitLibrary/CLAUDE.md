# Surl.Cryptography.Blowfish.UnitLibrary

Holds `Blowfish`: the Blowfish block cipher (Schneier, 1993) with the key schedules of
OpenBSD's `blf.c` - the standard one (`ExpandKey(key)`, `Blowfish_expand0state`) and the
salted one bcrypt's "eksblowfish" adds (`ExpandKey(data, key)`, `Blowfish_expandstate`) -
and one 8-byte block encrypted or decrypted, as bytes (`EncryptBlock`, `DecryptBlock`,
big-endian halves) or as the word pair bcrypt uses (`Encrypt`, `Decrypt`). `new
Blowfish(key)` keys it with the standard schedule and refuses keys outside 4 to 56 bytes;
`new Blowfish()` and `Initialize` leave it unkeyed on the digits of pi, for bcrypt, whose
64-byte keys the schedules stream cyclically. `ReadWord` is `Blowfish_stream2word` and
`Clear` zeroes the state. `BlowfishPiDigits` holds the initial P-array and S-boxes. The base
class library has no Blowfish, so it is hand-built (ADR-0061); BL-255 moved it here from
`Surl.Cryptography.BcryptPbkdf.UnitLibrary`, which now references this library.
`blowfish-cbc` (RFC 4253 section 6.3) is composed from it in `Surl.Protocol.Ssh`, not here:
chaining modes are not this library's.

It references nothing (ADR-0061); protocol servers may reference it as a horizontal
library. Bytes in, bytes out: never open a socket or a file, and never read a clock.

Every behaviour is tested against published vectors, with the source cited beside each
test: Eric Young's ECB, set_key and CBC vectors in Schneier's `vectors-2.txt`. Copying
Blowfish and its tests from the Curl port is allowed: code is not a verdict, and no
expected value comes from it (ADR-0003).
