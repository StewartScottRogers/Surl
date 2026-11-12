# Surl.Cryptography.Curve448.UnitLibrary

Holds GF(2^448 - 2^224 - 1) field arithmetic (`Field448`, internal) and the X448 function
(`X448`, RFC 7748 sections 5 and 6.2), hand-built because the base class library has no
X448 on any platform (ADR-0147). `Surl.Tls13.UnitLibrary` references it for the TLS 1.3
`x448` key-exchange group.

`Field448` keeps an element as eight unsigned 56-bit limbs, so limb i is bytes 7i to 7i + 6
of the 56-byte little-endian encoding, multiplies through `UInt128` columns and reduces with
2^448 = 2^224 + 1 (mod p). Both types are constant-time in their secrets: masks, never
branches or indexes chosen by a secret; the only branch in the arithmetic is on the public
exponent p - 2 in `Field448.Invert`. `X448.ScalarMultiply` clamps the scalar (clear the two
low bits, set the top bit of the last byte), decodes the u-coordinate modulo p without
masking any bit, and returns its result as computed: rejecting the all-zero shared secret
is the caller's (RFC 7748 section 6.2; RFC 8446 section 7.4.2).

It references nothing. Bytes in, bytes out: never open a socket or a file, and never read
a clock.

Every function is tested against RFC 7748's published vectors (section 5.2's two X448
vectors and its iterated vector after 1 and 1,000 iterations, and section 6.2's key pair
and shared secret), with the source cited beside each vector; the field tests check
reduction and inversion against p as section 4.2 defines it. No expected value comes from
the Curl port (ADR-0003); copying code from it is allowed, because code is not a verdict.
