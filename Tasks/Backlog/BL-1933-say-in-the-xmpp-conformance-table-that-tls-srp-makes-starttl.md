---
id: BL-1933
title: Say in the XMPP conformance table that TLS-SRP makes STARTTLS and xmpp work with no certificate
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1924]
touches: [Documentation/Conformance/Extended/Xmpp.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1933 — Say in the XMPP conformance table that TLS-SRP makes STARTTLS and xmpp work with no certificate

## Goal

The STARTTLS table in `Documentation/Conformance/Extended/Xmpp.md` is true under TLS-SRP.

## Context

Found while working BL-1924 (2026-11-27), read from source. Prose only. Line numbers are as of that date and
approximate. Under TLS-SRP, `ServerTlsComposition.IsTlsUpgradeAvailable` is true with no certificate (ADR-0122
decision 5), and `FindListenUrlWithoutCertificate` refuses neither an `xmpp` nor an `xmpps` listen URL (ADR-0122
Amendment 5: TLS-SRP secures TCP listeners).

- Row `Xmpp:RFC6120-5.3.1-1`, about line 50: "On `xmpp` with a certificate".
- Row `Xmpp:Surl-Tls-2`, about line 59: `xmpps` without a certificate, and `xmpp` without one and without a loosening
  option, "fail at start with `CertificateProblem` (58)", which TLS-SRP lifts.
  `ServerTlsCompositionTests.FindListenUrlWithoutCertificate_XmppUnderTlsSrp_IsNull` in
  `Application/Surl.Composition.UnitTests` covers the `xmpp` half of the exception.
- Row `Xmpp:RFC6120-5.4.2.2-1`, about line 55, already says "When the upgrade is unavailable" and needs no change.
- The names in "Covered by" (`ServeAsync_CertificateConfigured_OffersRequiredStartTlsAlone`,
  `CommandLineRunnerExtendedProtocolTests.RunAsync_XmppsSchemeWithoutACertificate_ExitsWithTheMissingCertificateCode`)
  are the tests' real names and stay as they are.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [ ] Row `Xmpp:RFC6120-5.3.1-1` states its condition as a certificate or TLS-SRP.
- [ ] Row `Xmpp:Surl-Tls-2` says the `(58)` refusals do not happen under TLS-SRP, and its "Covered by" adds `ServerTlsCompositionTests.FindListenUrlWithoutCertificate_XmppUnderTlsSrp_IsNull`.
- [ ] Every test named in the changed rows exists under that name in the solution's test projects.

## Notes

## Log

- 2026-11-27: Created.
