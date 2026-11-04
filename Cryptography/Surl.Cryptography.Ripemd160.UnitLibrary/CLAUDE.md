# Surl.Cryptography.Ripemd160.UnitLibrary

Holds RIPEMD-160 (Dobbertin, Bosselaers and Preneel, 1996) and HMAC-RIPEMD-160 (RFC 2286):
- `Ripemd160` hashes any message to 20 bytes, one-shot with `HashData` (into a destination
  span or as a new array) or in pieces with `AppendData` and `GetHashAndReset`, and zeroes
  its state and buffer on `Dispose`.
- `HmacRipemd160` is RFC 2104's HMAC over `Ripemd160` with a 64-byte block (a longer key is
  hashed first), one-shot with `HashData(key, message)` or keyed once and fed message after
  message; `Verify` compares a MAC in fixed time.

The base class library has no RIPEMD-160, and its `HMAC` cannot take a hash it does not
know, so both are hand-built here (ADR-0061). `hmac-ripemd160` and
`hmac-ripemd160@openssh.com` are composed from it in `Surl.Protocol.Ssh`, not here.

It references nothing (ADR-0061); protocol servers may reference it as a horizontal
library. Bytes in, bytes out: never open a socket or a file, and never read a clock.

Every behaviour is tested against published vectors, with the source cited beside each
test: the RIPEMD-160 authors' published vectors
(https://homes.esat.kuleuven.be/~bosselae/ripemd160.html) for the hash, and RFC 2286
section 2's seven test cases for the HMAC. Copying RIPEMD-160 and its tests from the Curl
port is allowed: code is not a verdict, and no expected value comes from it (ADR-0003).
