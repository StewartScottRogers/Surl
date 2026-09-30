# Surl.Kerberos.UnitLibrary

Hand-built Kerberos 5 for surl's Kerberos acceptor (ADR-0057 decision 6), which
`Surl.Authentication.UnitLibrary` will reference for Kerberos inside Negotiate and SASL
`GSSAPI` (BL-240).

Holds today: the public `KerberosEncryptionType` (enctypes 17, 18, 19 and 20, ADR-0057
decision 3) and, internal, the crypto those enctypes need - RFC 3961's `n-fold` (`NFold`) and
simplified-profile `DK` (`SimplifiedProfileKeyDerivation`), AES with ciphertext stealing over the
BCL's CBC (`AesCiphertextStealing`), RFC 8009's `KDF-HMAC-SHA2` (`KdfHmacSha2`), and one
`KerberosEncryptionProfile` per enctype (`AesCtsHmacSha1Profile` for RFC 3962,
`AesCtsHmacSha2Profile` for RFC 8009) that derives Kc, Ke and Ki, encrypts, decrypts and
checksums (checksum types 15, 16, 19 and 20). There is no string-to-key: keys come from a
keytab (ADR-0057 decision 1). The keytab, AP-REQ check, AP-REP, tokens and replay cache are
BL-239.

It references nothing but the shared framework (ADR-0002): never open a socket or a file. One
public type per file, namespace `Surl.Kerberos`. Every primitive is pinned by its RFC's
published vectors, with the source cited beside each vector; an integrity failure is a `false`
return, compared with `CryptographicOperations.FixedTimeEquals`, never an exception.
