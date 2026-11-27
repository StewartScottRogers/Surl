---
id: BL-1880
title: Serve the configured certificate on QUIC and DTLS listeners under TLS-SRP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1879, BL-1867]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Application/Surl.Console/CLAUDE.md, Documentation/Planning/Decisions/ADR-0122-how-surl-serves-https-over-tls-srp.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1880 — Serve the configured certificate on QUIC and DTLS listeners under TLS-SRP

## Goal

Under TLS-SRP, the `webtransport`, `coaps` and DNS-over-QUIC listeners serve the `--cert` or `--self-signed` certificate, and DNS over QUIC without one is refused at start with `SurlExitCode.CertificateProblem` (58).

## Context

Found while working BL-1867 (2026-11-27), read from source; nothing was run. Prove each case below with a failing test before changing anything; if a test shows a case does not happen, record that under Notes and leave that behaviour alone.

`ServerTlsComposition.ComposeTlsSrp` passes `null` as the `QuicCertificate`, even with `--cert` or `--self-signed`. TLS-SRP alone is refused without another listen URL that uses TLS (for example `https`), so each case has one.

- `webtransport` with `--self-signed`: the start succeeds, and `WebTransportProtocolServer.ServeAsync` returns without answering when its certificate source gives `null`.
- `coaps` with `--cert` or `--self-signed`: `DeferredCoapsProtocolServer` builds with no certificate, so only the pre-shared-key suites are offered, and `CoapsDtlsCertificate.FindRefusal` is given `null`.
- `--dns-transport quic`: `IsSecuredByTlsSrp` takes only the scheme and answers true for `dns`, so a start with no certificate is not refused; with or without a certificate, `ExtendedProtocolServers.ComposeDnsOverQuic` appears to throw `InvalidOperationException` on the first flow. `--dns-transport quic` is not among the options `TlsSrpCombination` refuses.

Decisions that already settle it: ADR-0122 decision 2 and Amendment 2 ("QUIC and DTLS still need a certificate. TLS-SRP secures TCP listeners only"), ADR-0092 decision 5 (DNS over QUIC), and `Application/Surl.Composition.UnitLibrary/CLAUDE.md` (DNS over QUIC "needs `--cert` or `--self-signed` (58)"). `--http3` and `--cacert` stay refused beside TLS-SRP. DNS over TLS and DNS over HTTPS are TCP listeners and still need no certificate. A `coaps` listen URL with an account and no certificate still serves pre-shared keys only.

Start in `ServerTlsComposition.cs` (`IsSecuredByTlsSrp`, `ComposeTlsSrp`, `ComposeQuicCertificate`; the QUIC key must be disposed with the composition) with tests in `ServerTlsCompositionTests.cs`, `CoapsCompositionTests.cs` and `DnsOverQuicCompositionTests.cs`. The comment above `ParseTlsSrp` in `ServerTlsCompositionTests.cs` still says the parser refuses `--cert` and `--self-signed` beside TLS-SRP, which BL-1099 lifted.

Stand-in: a test that runs HTTPS over TLS-SRP beside WebTransport, CoAP or DNS over QUIC in one instance gets a working QUIC or DTLS listener. No store, event or fault changes.

## Acceptance criteria

- [x] A named test shows that under TLS-SRP with `--self-signed`, composing a `webtransport` listen URL, and one composing a `--dns-transport quic` listen URL, gives a non-null `QuicCertificate` whose first chain entry matches `ThrowawayCertificateFingerprint`; both were seen to fail first (say so under Notes).
- [x] A named test shows that under TLS-SRP with `--cert` or `--self-signed`, a `coaps` listen URL gets that certificate, and a certificate whose key DTLS cannot use ends the start with 58 as it does without TLS-SRP.
- [x] A named test shows that `SurlServerComposition.ComposeAsync` with TLS-SRP, an `https` listen URL, `--dns-transport quic` and neither `--cert` nor `--self-signed` returns `SurlExitCode.CertificateProblem`, and that `--dns-transport tls` in the same setup is not refused.
- [x] The `ServerTlsComposition` text in `Application/Surl.Composition.UnitLibrary/CLAUDE.md` and in `Application/Surl.Console/CLAUDE.md` (the `ServerTlsComposition` bullet) names DNS over QUIC beside `webtransport` and `coaps` as needing a certificate under TLS-SRP, and no longer limits the DTLS key refusal to starts without TLS-SRP.
- [x] ADR-0122 gains a dated amendment, marked "Decided by Claude under Stewart's delegation", stating what each of the three listeners does under TLS-SRP.
- [x] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean and the fast tests of `Application/Surl.Composition.UnitTests` pass.

## Notes

- Seen to fail before the change: `ServerTlsCompositionTests.Compose_TlsSrpWithSelfSignedAndAQuicListenUrl_HasTheThrowawayCertificateForQuic` (both rows, `webtransport` and DNS over QUIC) and `Dispose_TlsSrpWithAQuicCertificate_DisposesItsKey`; `CoapsCompositionTests.Compose_CoapsUnderTlsSrpWithSelfSigned_HasTheThrowawayCertificate` and `...WithCertAndKey_HasThatCertificate`; `DnsOverQuicCompositionTests.ComposeAsync_QuicUnderTlsSrpWithoutACertificate_IsRefusedCertificateProblem` (the start succeeded).
- One case did not happen, so its behaviour is unchanged: a DTLS-unusable key under TLS-SRP already ended the start with 58. TLS-SRP refuses every non-RSA key, and the loader refuses RSA under 2048 bits, so `CoapsDtlsCertificate`'s own refusal cannot be reached under TLS-SRP. `ComposeAsync_CoapsUnderTlsSrpWithACertificateDtlsCannotServe_FailsWithCertificateProblem` (ECDSA P-521) pins it; it passed before the change.
- Not separately observed: the `InvalidOperationException` on a first DNS over QUIC flow. It follows from the null `QuicCertificate`, which the failing composition test showed.
- No upstream curl measurement: no byte Surl sends or expects changed.
- Pipeline: planned, implemented and reviewed in the session without subagents; the change is one method group in one library.
- Filed BL-1916: the `--aihelp tls` page still says TLS-SRP needs no certificate on every TLS listen URL (`Surl.Cli` was in BL-1902's `touches`).

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. Under TLS-SRP, webtransport, coaps and DNS over QUIC serve the --cert or --self-signed certificate, and DNS over QUIC without one is refused (58).
