# Surl.Cryptography.UnitLibrary

Phase 1.

Hand-built cryptographic primitives the base class library lacks on at least one of
Windows, Linux and macOS, for the authentication verifiers. A primitive the BCL offers on
all three platforms is taken from `System.Security.Cryptography` and never rebuilt here.

Holds today: `Sha512Slash256` (FIPS 180-4 SHA-512/256, one-shot `HashData`), for the
Digest verifier's `algorithm=SHA-512-256` (RFC 7616), and `Md4` (RFC 1320, one-shot
`HashData`), for the NTLM verifier's NT hash; MD4 is broken as a general hash and is used
only there. And `Des` (FIPS 46-3, one-block `EncryptBlock`), for NTLMv1's `LMOWFv1` and
`DESL` (BL-291): the BCL has DES on all three platforms but refuses its weak and semi-weak
keys, which NTLMv1 needs (an empty password's LM key is the all-zero weak key), so DES is
built here, encryption only.

The SSH server's primitives do not land here (ADR-0048). Each has its own library:
`Surl.Cryptography.Curve25519.UnitLibrary` (X25519), `Surl.Cryptography.Ed25519.UnitLibrary`,
`Surl.Cryptography.ChaCha20.UnitLibrary` and `Surl.Cryptography.Poly1305.UnitLibrary`.

It references nothing (ADR-0002). Bytes in, bytes out: never open a socket or a file.
Every primitive is tested against its specification's published vectors, with the source
cited beside each vector. Copying a primitive and its tests from the Curl port is allowed:
code is not a verdict (ADR-0003).
