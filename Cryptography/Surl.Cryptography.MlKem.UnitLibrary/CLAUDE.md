# Surl.Cryptography.MlKem.UnitLibrary

Holds `MlKem768` and `MlKem1024`: ML-KEM-768 and ML-KEM-1024 (FIPS 203), hand-built because the
base class library's `MLKem` needs OpenSSL 3.5 on Linux or a Windows Insider build and does not
exist on Apple platforms (ADR-0082 decision 2). Both have the same members: `GenerateKey` is
ML-KEM.KeyGen_internal, `TryEncapsulate` is ML-KEM.Encaps_internal behind section 7.2's modulus
check (false when the encapsulation key fails it), and `TryDecapsulate` is
ML-KEM.Decaps_internal behind section 7.3's hash check, with a branch-free implicit-rejection
select. The caller draws every random seed (d, z and m); this library never does.
`Surl.Tls13` encapsulates to the client's key with `MlKem768` for the X25519MLKEM768 group and
with `MlKem1024` for the SecP384r1MLKEM1024 group (ADR-0147 decision 3).

The internal parts, visible to the tests only: `Keccak` (Keccak-f[1600]), `KeccakSponge`,
`Sha3` (SHA3-256, SHA3-512, SHAKE128, SHAKE256 - hand-built too, since the BCL has none of them on
macOS), `MlKemPolynomial` (FIPS 203 Algorithms 5 to 12: encoding, sampling, NTT), `KPke`
(Algorithms 13 to 15, one instance per parameter set: `KPke.ForMlKem768` with k = 3, d_u = 10,
d_v = 4 and `KPke.ForMlKem1024` with k = 4, d_u = 11, d_v = 5) and `MlKemKeyEncapsulation`
(Algorithms 16 to 18 and the section 7 checks over one `KPke`), which `MlKem768` and `MlKem1024`
each delegate to. A new parameter set is a new `KPke` instance and a new public facade, not a
copy of the algorithms. `SamplePolyCBD` is built for eta = 2 only, which is right for both
parameter sets here; ML-KEM-512's eta_1 = 3 would need it widened.

It references nothing (ADR-0082 decision 2); `Surl.Tls13.UnitLibrary` may reference it. Bytes
in, bytes out: never open a socket or a file, and never read a clock.

Every expected value in its tests comes from NIST: the ACVP ML-KEM-768 and ML-KEM-1024 keyGen,
encapsulation, decapsulation and key-check vectors in `Fixtures/Acvp/ML-KEM-768.json` and
`Fixtures/Acvp/ML-KEM-1024.json`
(https://github.com/usnistgov/ACVP-Server/tree/master/gen-val/json-files), and NIST's SHA-3
example values. Never from the Curl port (ADR-0003).
