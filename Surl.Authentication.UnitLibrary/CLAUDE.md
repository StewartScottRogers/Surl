# Surl.Authentication.UnitLibrary

Phase 1.

The server side of every authentication scheme upstream curl can send: issues the
challenge (`WWW-Authenticate`, `Proxy-Authenticate`) and verifies the answer for Basic,
Digest, NTLM, Negotiate (SPNEGO and Kerberos), Bearer and AWS Signature Version 4, and for
the SASL mechanisms curl uses with the mail protocols. Anything time-dependent (nonces,
signature windows) takes an injected `TimeProvider`.

This library references `Surl.Protocol.Abstractions.UnitLibrary` and no protocol server.
Protocol servers receive what it provides through the contracts in Abstractions.
