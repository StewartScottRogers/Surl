---
id: BL-1923
title: Say in --aihelp that STARTTLS-style upgrades and their TLS schemes work under TLS-SRP with no certificate
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1923 — Say in --aihelp that STARTTLS-style upgrades and their TLS schemes work under TLS-SRP with no certificate

## Goal

The `--aihelp` pages of the servers that upgrade a plaintext connection to TLS say what happens under TLS-SRP, and no
longer say a certificate is the only way to get TLS there.

## Context

Found while working BL-1881 (2026-11-27), read from source. Help text and the tests that pin it; no behaviour change.

- Under TLS-SRP (`--tlsauthtype SRP` or `--tlsuser`), `ServerTlsComposition.IsTlsUpgradeAvailable` is true with no
  certificate, so `amqp1`, `ftp`, `imap`, `ldap`, `pop3`, `smtp` and `xmpp` offer their upgrade (ADR-0122 decision 5).
  `IsSecuredByTlsSrp` also lifts the certificate requirement from their TCP TLS twins and from `xmpp` itself.
- `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs` says otherwise in the paragraphs of those topics: the upgrade
  sentences for `amqp1` ("answered only when `--cert` or `--self-signed` is given"), `ftp`, `pop3`, `imap`, `ldap`,
  `smtp` ("without a certificate ... is answered/refused") and, about line 389, `xmpp` ("Without a certificate `xmpp`
  never offers TLS", and is refused at start, 58). The same paragraphs say `amqp1s`, `ftps`, `pop3s`, `imaps`,
  `ldaps`, `smtps` and `xmpps` need `--cert` or `--self-signed`.
- `AiHelpTextTests.Answer_XmppTopic_SaysAListenUrlWithoutACertificateExits58UnlessLoosened` pins part of the `xmpp`
  sentence.
- BL-1916 corrects the TLS-SRP paragraph of the `tls` topic (QUIC and DTLS still need a certificate); leave it to that
  task.

No store, event or fault is involved: help text only.

## Acceptance criteria

- [ ] In each of the `amqp1`, `ftp`, `imap`, `ldap`, `pop3` and `smtp` paragraphs named above, the upgrade is said to be offered with `--cert`, `--self-signed` or TLS-SRP, and the refusal answer is said to apply only with none of them.
- [ ] The `xmpp` paragraph says STARTTLS is offered with a certificate or under TLS-SRP, and that the 58 refusal of an `xmpp` listen URL does not happen under TLS-SRP.
- [ ] No sentence in those seven topics says their TLS-from-the-first-byte scheme needs `--cert` or `--self-signed` without excepting TLS-SRP.
- [ ] A named test in `Surl.Cli.UnitTests` pins the TLS-SRP wording of each of the seven topics, and the pinned `xmpp` test matches the new text.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
