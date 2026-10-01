# Surl.Kerberos.UnitLibrary

Hand-built Kerberos 5 for surl's Kerberos acceptor (ADR-0057 decision 6), which
`Surl.Authentication.UnitLibrary` will reference for Kerberos inside Negotiate and SASL
`GSSAPI` (BL-240). The hand-built test KDC, `Surl.Kerberos.TestKdc.UnitLibrary` (ADR-0065
decision 1), references it too and reads its internals (the enctype profiles, `KerberosDer`
and the message readers) through `InternalsVisibleTo`, as its tests do.

Holds today: the public `KerberosEncryptionType` (enctypes 17, 18, 19 and 20, ADR-0057
decision 3) and, internal, the crypto those enctypes need - RFC 3961's `n-fold` (`NFold`) and
simplified-profile `DK` (`SimplifiedProfileKeyDerivation`), AES with ciphertext stealing over the
BCL's CBC (`AesCiphertextStealing`), RFC 8009's `KDF-HMAC-SHA2` (`KdfHmacSha2`), and one
`KerberosEncryptionProfile` per enctype (`AesCtsHmacSha1Profile` for RFC 3962,
`AesCtsHmacSha2Profile` for RFC 8009) that derives Kc, Ke and Ki, encrypts, decrypts and
checksums (checksum types 15, 16, 19 and 20). There is no string-to-key: keys come from a
keytab (ADR-0057 decision 1).

On top of that, the acceptor (BL-239, ADR-0057 decisions 1 to 5 and 7), public:
`KerberosKeytab.Read` reads an MIT keytab's bytes into `KerberosKeytabEntry`s, reporting
entries of other enctypes as `KerberosKeytabSkippedEntry`s and malformed bytes by offset in a
`KerberosKeytabReadResult`; `KerberosPrincipalName` prints the RFC 1964 display form;
`KerberosAcceptor.Accept` (bare token) and `AcceptInsideSpnego` (which also takes Microsoft's
Kerberos OID) check an AP-REQ and return a `KerberosAcceptResult`, a
`KerberosSecurityContext` or a refusal reason; the context makes the AP-REP and the RFC 4121
wrap and MIC tokens; `KerberosReplayCache` (one per process) refuses replayed
authenticators; `IKerberosRandomSource` supplies the AP-REP's random bytes. Internal:
`GssApiToken` (framing), `KerberosDer` and one reader per RFC 4120 message
(`KerberosApRequest`, `KerberosTicketPart`, `KerberosAuthenticatorPart`,
`KerberosEncryptedData`, `KerberosEncryptionKey`), `GssApiChecksum` (the `0x8003` checksum)
and `KeytabByteReader`. Malformed DER is an `AsnContentException` inside and the refusal
`malformed token` outside; refusal reasons never carry a key byte or a decrypted field.

It references nothing but the shared framework (ADR-0002): never open a socket or a file. One
public type per file, namespace `Surl.Kerberos`. Every primitive is pinned by its RFC's
published vectors, with the source cited beside each vector; an integrity failure is a `false`
return, compared with `CryptographicOperations.FixedTimeEquals`, never an exception.

`KerberosSecurityContext` also reads the authenticator checksum's GSS-API flags
(`GssApiChecksum.ReadFlags`) into `IsConfidentialityRequested` and `IsIntegrityRequested`, and
`Seal` makes an acceptor wrap token with confidentiality (`EC` 0, `RRC` 28, as Windows sends it),
for LDAP's Kerberos security layer (BL-327, ADR-0072 Amendment 1).
