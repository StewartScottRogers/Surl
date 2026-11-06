# Surl.Cryptography.MlKem.UnitLibrary

Holds `MlKem768`: ML-KEM-768 (FIPS 203), hand-built because the base class library's `MLKem`
needs OpenSSL 3.5 on Linux or a Windows Insider build and does not exist on Apple platforms
(ADR-0082 decision 2). `GenerateKey` is ML-KEM.KeyGen_internal, `TryEncapsulate` is
ML-KEM.Encaps_internal behind section 7.2's modulus check (false when the encapsulation key
fails it), and `TryDecapsulate` is ML-KEM.Decaps_internal behind section 7.3's hash check, with
a branch-free implicit-rejection select. The caller draws every random seed (d, z and m); this
library never does. `Surl.Tls13` encapsulates to the client's key for the X25519MLKEM768 group.

The internal parts, visible to the tests only: `Keccak` (Keccak-f[1600]), `KeccakSponge`,
`Sha3` (SHA3-256, SHA3-512, SHAKE128, SHAKE256 - hand-built too, since the BCL has none of them on
macOS), `MlKemPolynomial` (FIPS 203 Algorithms 5 to 12: encoding, sampling, NTT) and `KPke`
(Algorithms 13 to 15).

It references nothing (ADR-0082 decision 2); `Surl.Tls13.UnitLibrary` may reference it. Bytes
in, bytes out: never open a socket or a file, and never read a clock.

Every expected value in its tests comes from NIST: the ACVP ML-KEM-768 keyGen, encapsulation,
decapsulation and key-check vectors in `Fixtures/Acvp/ML-KEM-768.json`
(https://github.com/usnistgov/ACVP-Server/tree/master/gen-val/json-files), and NIST's SHA-3
example values. Never from the Curl port (ADR-0003).
