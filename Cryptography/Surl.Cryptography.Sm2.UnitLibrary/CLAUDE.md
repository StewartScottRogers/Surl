# Surl.Cryptography.Sm2.UnitLibrary

Holds `CurveSm2`: elliptic-curve Diffie-Hellman on curveSM2, the SM2 recommended curve of
GB/T 32918.5-2017 (TLS NamedGroup 41, 0x0029, RFC 8998), hand-built because the base class
library has no SM2 curve on every platform - explicit curves are unavailable on macOS
(ADR-0147). A public key is the uncompressed point 0x04 || x || y (65 bytes) and the shared
secret is the 32-byte x-coordinate of the product, as RFC 8446 sections 4.2.8.2 and 7.4.2 have
it for the NIST curves; RFC 8998 section 3.2.1 fixes the curve. `ComputePublicKey` reads any
32 bytes as the scalar (value mod (n - 1)) + 1. `TryComputeSharedSecret` returns false, writing
nothing, for a peer point that is not 0x04 || x || y with x, y < p on the curve, or a product at
infinity; a span of the wrong size is an `ArgumentException`.

The internal parts, visible to the tests only: `CurveSm2Parameters` (p, a, b, n and G, each
cited to OSCCA's published recommended-curve parameters, GM/T 0003.5-2012 and RFC 8998),
`Sm2AffinePoint`, `Sm2JacobianPoint` and `Sm2PointArithmetic` (curve membership, Jacobian
doubling and addition, double-and-add, conversion to affine).

**Not constant-time.** The arithmetic is `System.Numerics.BigInteger` and the scalar's bits
choose branches and running time, and a `BigInteger` holding a secret cannot be zeroed. That is
acceptable here: Surl is a loopback test server and its curveSM2 keys are ephemeral, one per
handshake. Do not reuse this library where a timing side channel matters.

It references nothing (ADR-0147). `Surl.Tls13.UnitLibrary` references it for the curveSM2
group, and protocol servers may reference it as a horizontal library. Bytes in, bytes out:
never open a socket or a file, and never read a clock.

Every expected value in its tests comes from a published source, cited beside it: the key
pairs and points of GM/T 0003.5-2012 Annexes A and B in OSCCA's English text
(http://www.gmbz.org.cn/upload/2018-07-24/1532401863206085511.pdf), which use the recommended
curve. Never from the Curl port (ADR-0003).
