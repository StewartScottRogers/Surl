---
id: BL-1068
title: Open TLS client connections to an LDAP account source in Surl.Networking
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-671]
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-11
completed: 2026-11-12
---
# BL-1068 — Open TLS client connections to an LDAP account source in Surl.Networking

## Goal

`Surl.Networking` offers a public TLS client connector that opens a TCP connection to an operator-named host and
port, runs a verified TLS 1.2 or 1.3 client handshake at once (`ldaps`) or later on the open stream (StartTLS), so
`Surl.Composition` can adapt it to `Surl.Authentication`'s `ILdapDirectoryConnector` (ADR-0149 decision 11).

## Context

- Decision: [ADR-0149](../../Documentation/Planning/Decisions/ADR-0149-how-an-ldap-directory-serves-as-an-optional-account-source.md)
  decisions 5 (TLS, RFC 4513 section 3.1.3 server identity) and 11 (where the code lives).
- Only `Surl.Networking` constructs a `Socket` or an `SslStream` (root `CLAUDE.md`). Resolve and connect as
  `SocketOriginConnector` does (`Foundation/Surl.Networking.UnitLibrary/SocketOriginConnector.cs`), but with **no
  allowed-origin rule**: the operator names the directory. `Surl.Networking` must not reference
  `Surl.Authentication`; the connector's types are its own, and BL-673 writes the adapter in `Surl.Composition`.
- Trust: the system roots plus the PEM certificates given (`ca-file`), built with `X509Chain` and
  `X509ChainTrustMode.CustomRootTrust` only when certificates are given beside the system store; host name or IP
  literal checked against the certificate; no option skips either check. TLS 1.2 and 1.3 only (ADR-0006 section 4).
  No client certificate.
- The timeout and cancellation come from the caller; time on the injected `TimeProvider`.

## Acceptance criteria

- [x] Named tests in `Foundation/Surl.Networking.UnitTests`, against an in-process loopback `SslStream` server, show:
      a handshake at connect accepted for a certificate trusted through given PEM certificates; a handshake run later
      on the same open stream (StartTLS) after clear bytes were exchanged; an untrusted chain refused; a certificate
      not naming the host refused; a refused connection and a timeout each reported as a distinct failure.
- [x] No failure's message holds anything the caller sent over the connection.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green on all three
      platforms' rules (no Windows-only certificate store use outside an `[OSCondition]` test), and the library keeps
      100% line and branch coverage.

## Notes

- Filed by BL-671 (ADR-0149 decision 11). BL-673 depends on it.
- Delivered as `TlsClientConnector`, `TlsClientConnection`, `TlsServerIdentityCheck`, `TlsClientFailure`,
  `TlsClientException` in `Surl.Networking.UnitLibrary`; tests in `TlsClientConnectorTests` (19 tests,
  in-memory `SslStream` server plus one loopback-socket case). Library keeps 100% line and branch coverage;
  every method is far below complexity 10.
- Choices (defaults taken, within ADR-0149 decision 5): the chain is first judged by the platform against the
  system roots; only if that fails is it rebuilt with `CustomRootTrust` over the given certificates (union of
  trust); name mismatch is reported after chain trust. Revocation is not checked (no CRL/OCSP traffic to a
  test directory). The handshake after StartTLS takes the caller's token; the timeout covers connect plus
  handshake only for `secureAtOnce`. Test certificates carry a DNS and a 127.0.0.1 SAN.

## Log

- 2026-11-11: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. Surl.Networking offers TlsClientConnector: verified TLS 1.2/1.3 client connections to an operator-named host, at once or via StartTLS
