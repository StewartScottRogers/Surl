---
id: BL-1924
title: Correct the certificate-only TLS upgrade comments in Surl.Composition and the XMPP server
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1881]
touches: [Application/Surl.Composition.UnitLibrary/ComposedProtocolServices.cs, Application/Surl.Composition.UnitLibrary/SurlServerComposition.cs, Application/Surl.Composition.UnitLibrary/ServerTlsComposition.cs, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1924 — Correct the certificate-only TLS upgrade comments in Surl.Composition and the XMPP server

## Goal

The comments on the TLS upgrade in `Surl.Composition` and the XMPP server say it is available with a certificate or
under TLS-SRP, and the `ClientTrustAnchors` summary names every server that uses the anchors.

## Context

Found while working BL-1881 (2026-11-27), read from source. Comment and project-notes text only; no behaviour change.
Line numbers are as of that date. The composition passes `ServerTlsComposition.IsTlsUpgradeAvailable` (a certificate,
or TLS-SRP) as `isTlsUpgradeAvailable` (`SurlServerComposition.Start.cs`, about line 430).

- `Application/Surl.Composition.UnitLibrary/ComposedProtocolServices.cs`: the `isTlsUpgradeAvailable` param (about
  line 27) and the `IsTlsUpgradeAvailable` summary (about line 193) say "a certificate is configured".
- `Application/Surl.Composition.UnitLibrary/SurlServerComposition.cs`: the same at about lines 241, 603 (the
  `LoadLdapServerAsync` summary), 608 and 647.
- `Application/Surl.Composition.UnitLibrary/ServerTlsComposition.cs`: the `ClientTrustAnchors` summary names only a
  `coaps` client certificate. `SurlServerComposition.Start.cs` (about lines 635-637) also gives the anchors to
  `ComposeHttp3ProtocolServer`. BL-1881 edits other comments in this file first.
- `Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary`: `XmppServer.cs` (remarks, about lines 19-33),
  `XmppServerConnection.cs` (comments about lines 83 and 378), `XmppServerOptions.cs` (about lines 26-35) and
  `CLAUDE.md` (the STARTTLS section) tie STARTTLS to a certificate alone. The same `CLAUDE.md`'s opening says `surl`
  does not serve `xmpp` yet; `ExtendedProtocolServers.Registrations` has it.
- The log note "A <starttls/> arrived and no certificate is configured; ..." (`XmppServerConnection.cs`, about line
  390) is written only when no upgrade is available, so with neither a certificate nor TLS-SRP: it is not false, and
  it stays as it is.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [x] The `isTlsUpgradeAvailable` and `IsTlsUpgradeAvailable` comments in `ComposedProtocolServices.cs` and `SurlServerComposition.cs`, and the `LoadLdapServerAsync` summary, name both a certificate and TLS-SRP.
- [x] The `ServerTlsComposition.ClientTrustAnchors` summary names both the `coaps` server and the HTTP/3 server.
- [x] No comment or `CLAUDE.md` line in the XMPP library says STARTTLS is offered or required only with a certificate.
- [x] The XMPP library's `CLAUDE.md` no longer says `surl` does not serve `xmpp`.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the only changed `.cs` lines are comments.

## Notes

- 2026-11-27: Comments and the XMPP `CLAUDE.md` corrected in the seven files in `git diff`. The `ClientTrustAnchors`
  summary also names the TCP listeners (through `Settings`) and when there are no anchors. The
  `XmppServerOptions.Default` summary said "plain-text logins refused", which the default policy does not do; it now
  says both options are off.
- The log note "no certificate is configured" is unchanged, as Context decides.
- Verified: build 0 warnings, `dotnet format --verify-no-changes` exit 0, fast tests all passed (3 skipped:
  1 in `Surl.Content`, 2 in `Surl.Networking`).
- The same certificate-only claim remains outside `touches`; `--aihelp` is BL-1923, the rest is filed separately.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The TLS upgrade comments in Surl.Composition and the XMPP server name a certificate or TLS-SRP, ClientTrustAnchors names coaps and HTTP/3, and the XMPP notes say surl serves xmpp
