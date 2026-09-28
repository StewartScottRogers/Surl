# Surl.Cryptography.UnitLibrary

Phase 2, when the SSH server needs it.

Hand-built cryptographic primitives the base class library lacks on at least one of
Windows, Linux and macOS, first needed by the SSH server: Curve25519 key agreement,
Ed25519
host keys, ChaCha20-Poly1305 and whatever else a server-side peer of upstream curl's
libssh2 build needs. A primitive the BCL offers on all three platforms is taken from
`System.Security.Cryptography` and never rebuilt here.

It references nothing (ADR-0002). Bytes in, bytes out: never open a socket or a file.
Every primitive is tested against its specification's published vectors, with the source
cited beside each vector. Copying a primitive and its tests from the Curl port is allowed:
code is not a verdict (ADR-0003).
