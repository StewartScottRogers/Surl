# Surl.Cryptography.Ed25519.UnitLibrary

Holds Ed25519 (RFC 8032 section 5.1) for the SSH server's `ssh-ed25519` host keys and
user keys, hand-built because the base class library has no Ed25519 on any platform
(ADR-0048). Its SHA-512 comes from `System.Security.Cryptography` and is never rebuilt here.

- `Ed25519` (public): `ComputePublicKey(seed)`, `Sign(seed, message)` and
  `Verify(publicKey, message, signature)`. Verification is cofactorless and refuses an S
  not below L and a public key that does not decode, a non-canonical y included.
- `Edwards25519` (internal): edwards25519 points in extended coordinates - addition,
  a masked double-and-add scalar multiplication, encoding and decoding.
- `Scalar25519` (internal): reduction and multiply-add modulo the group order L.

Key derivation and signing are constant-time in the seed and nonce; decoding and
verification read public data only and are not.

It references `Surl.Cryptography.Curve25519.UnitLibrary` for the field arithmetic and
nothing else (ADR-0048); protocol servers may reference it as a horizontal library.
Bytes in, bytes out: never open a socket or a file, and never read a clock.

Every function is tested against RFC 8032's published vectors (section 7.1), with the
source cited beside each vector. Copying the Edwards-curve, scalar and Ed25519 code and
their tests from the Curl port is allowed: code is not a verdict, and no expected value
comes from it (ADR-0003).
