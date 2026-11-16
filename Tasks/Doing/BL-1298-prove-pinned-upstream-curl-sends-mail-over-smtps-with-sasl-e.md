---
id: BL-1298
title: Prove pinned upstream curl sends mail over smtps with SASL EXTERNAL and a client certificate
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Planning/Decisions/ADR-0049-the-mail-servers-sasl-and-apop-logins.md]
requirement: FR-046
created: 2026-11-16
completed:
---
# BL-1298 — Prove pinned upstream curl sends mail over smtps with SASL EXTERNAL and a client certificate

## Goal

`Integration` tests show a pinned upstream curl 8.21.0 build presenting a TLS client certificate and logging in to `surl smtps://` with SASL `EXTERNAL`, so the mail servers' `EXTERNAL` is proven against upstream curl, not by unit tests only.

## Context

- The Roadmap's Milestone 3 and the product overview ("Built for Phase 3") say: "SASL `EXTERNAL` over the mail servers is proven by unit tests only." BL-216 built it; ADR-0049 decision 4 says how: `EXTERNAL` logs in as the verified TLS client certificate (`--cacert` makes the handshake require one chaining to its anchors, ADR-0010 section 5), the identity is the certificate's subject simple name, the login is accepted when an account of exactly that name exists (its password unused) and curl's authorization identity (the `-u` user name) is empty or equal to it; otherwise `RefusedCredentials`. Amendment 1: with no client certificate, `EXTERNAL` is not offered and a forced `AUTH EXTERNAL` gets `RefusedMechanism`.
- curl's bytes are measured already: ADR-0049's table row "`EXTERNAL`, `-u user:`": `AUTH EXTERNAL`, `< 334 `, `> dXNlcg==`, or `AUTH EXTERNAL dXNlcg==` with an initial response. curl picks `EXTERNAL` only when forced with `--login-options AUTH=EXTERNAL` (curl 8.21.0 manpage, `--login-options`: IMAP, POP3 and SMTP).
- Which build: the Windows reference build uses Schannel, which takes a client certificate from a Windows certificate store rather than a PEM file (curl manpage, `--cert`, checked against the 8.23.1 page), and tests must stay platform-neutral. So run curl with `PinnedUpstreamCurl.RunBuildLinkedAgainstAsync(TestContext, "OpenSSL", ...)` (`Conformance/Surl.Conformance.UnitTests/PinnedUpstreamCurl.cs`): on `win-x64` that is the pinned static-curl 8.21.0 OpenSSL build (ADR-0030), on Linux and macOS the reference build; Inconclusive where none is installed. Record this choice and why in ADR-0049 as an amendment.
- Start from `UpstreamCurlBindsAndSearchesSurlOverOpenLdapTests.SaslBind_ExternalWithAClientCertificateOverLdaps_Exits0WithTheBaseEntry` and `SaslBind_ExternalWithoutAClientCertificate_Exits67LoginDenied`: `TestCertificateAuthority.Create()` gives the CA, server and client (`ClientCommonName` = `alice`) PEM files; surl gets `--cert`, `--key` and `--cacert` and an account named `alice`; curl gets `--cacert`, `--cert`, `--key`, `--login-options AUTH=EXTERNAL -u alice:`. For the send itself follow `UpstreamCurlSendsMailToSurlOverSmtpTests` (`StartSurlAsync("smtps", dataDirectory, ...)`, `--mail-from`, `--mail-rcpt`, `-T`), adding the tests to that class.
- Stand-in: the mail lands in the shared mail store (ADR-0100), where a test reading it over IMAP or POP3 (BL-1299, BL-1300) or through `Surl.Api` finds it; the login is judged by `AuthenticationPolicy` (`Foundation/Surl.Authentication.UnitLibrary`), which publishes it on the instance's event stream as it does every mail login. No new store, event or fault.

## Acceptance criteria

- [ ] `UpstreamCurlSendsMailToSurlOverSmtpTests` has `SendMail_SaslExternalWithAClientCertificateOverSmtps_Exits0AndStoresTheMail`: curl exits 0 and the stored message in `alice`'s mailbox holds the bytes sent; `[TestCategory("Integration")]`.
- [ ] It has `SendMail_SaslExternalWithoutAClientCertificate_<measured exit>`: surl with `--self-signed` and no `--cacert`, curl with `-k`, forced `AUTH=EXTERNAL`; asserts the exit code and stderr curl gave when run, and that nothing was stored.
- [ ] It has `SendMail_SaslExternalForACertificateWithNoAccount_<measured exit>`: no account named `alice`; asserts the measured exit code.
- [ ] `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~SaslExternal"` passes on Windows with the static-curl OpenSSL build installed; `dotnet build Conformance/Surl.Conformance.UnitTests -warnaserror` is clean and the fast run is unchanged.
- [ ] ADR-0049 has an amendment naming the three tests, the build each platform uses and why, and the exit codes measured.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
