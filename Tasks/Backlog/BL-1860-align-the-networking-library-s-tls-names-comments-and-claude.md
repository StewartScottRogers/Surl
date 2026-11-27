---
id: BL-1860
title: Align the Networking library's TLS names, comments and CLAUDE.md with its three handshake paths
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1849, BL-1859]
touches: [Foundation/Surl.Networking.UnitLibrary]
requirement: none
created: 2026-11-27
completed:
---
# BL-1860 — Align the Networking library's TLS names, comments and CLAUDE.md with its three handshake paths

## Goal

The Networking library's names, comments and `CLAUDE.md` say which of `TlsSrpStream`, `Tls13ServerStream` and
`SslStream` serves a handshake, and which statements hold for `SslStream` only.

## Context

Found during BL-1849 (checked against the code on 2026-11-27). BL-1849 already corrects the XML comment on
`ServerTlsHandshake.AuthenticateAsync` and the `CLAUDE.md` TLS 1.3 bullet; BL-1859 makes `close_notify` true of the
hand-built stream. All paths are in `Foundation/Surl.Networking.UnitLibrary`. Comments, prose and one private
method name only; no behaviour changes.

- `ServerTlsHandshake.cs`, the code comment above `AuthenticateTls13OnlyWithHandBuiltStreamAsync` says "any other
  client ... goes to SslStream". On a listener limited to TLS 1.3 the method serves every ClientHello that offers
  TLS 1.3 with the hand-built stream and refuses a parsed one without it with a `protocol_version` alert.
- That method's name says it authenticates TLS 1.3 only with the hand-built stream, yet it can hand the connection
  to `SslStream` or refuse it. It is private, with one caller and no other reference; a name such as
  `AuthenticateByClientHelloAsync` says what it does.
- `ClientHelloPeek.cs`: the class summary speaks only of a ClientHello "that only a TLS 1.3 server can answer" and
  omits `OffersTls13`, which the TLS-1.3-only listener uses.
- `ServerTlsSettings.cs`: the class summary and the `TlsSrpAccounts` summary say certificate settings serve through
  `SslStream`, leaving out `Tls13ServerStream` (`CanServeTls13Stream`, `CreateTls13Options`).
- `StreamConnection.cs`: the class summary says an upgraded connection reads and writes through the `SslStream`; it
  uses whichever of the three streams the handshake returned.
- `CLAUDE.md`, TLS-SRP bullet: says `ForTlsSrp(accounts, random)` makes settings with no certificate. It takes an
  optional `TlsSrpServerCertificate`, which `ServerTlsComposition.ComposeTlsSrp` in
  `Application/Surl.Composition.UnitLibrary` passes when `--cert` or `--self-signed` gives one.
- `CLAUDE.md`, "TLS minimums" bullet: "whose `AcceptedProtocols` every handshake enables", "Every handshake refuses
  renegotiation" with `AllowRenegotiation = false`, and the operating system's cipher suites describe
  `CreateAuthenticationOptions`, the `SslStream` options, only. TLS-SRP ignores `AcceptedVersions` and is TLS 1.2
  only (`TlsSrpStream` answers a renegotiation attempt with a `no_renegotiation` warning); the hand-built stream is
  TLS 1.3, which has no renegotiation, with `Surl.Tls13`'s suites.
- `CLAUDE.md`, "Reading the TLS option files" bullet: says `Surl.Console` maps `TlsFileLoadException` to exit codes;
  `ServerTlsComposition.DescribeTlsFileFailure` in `Surl.Composition` does.

## Acceptance criteria

- [ ] The method is renamed to a name that covers its three outcomes (hand-built stream, `SslStream`, refusal), and
      the comment above it states each, the TLS-1.3-only listener included.
- [ ] The `ClientHelloPeek` summary says it reports both `OffersOnlyTls13` and `OffersTls13`.
- [ ] The summaries of `ServerTlsSettings`, `ServerTlsSettings.TlsSrpAccounts` and `StreamConnection` name every
      stream that can serve under them.
- [ ] The `CLAUDE.md` TLS-SRP and "TLS minimums" bullets give the optional certificate and say which statements
      hold for `SslStream` only.
- [ ] The `CLAUDE.md` file-reading bullet names `Surl.Composition` as what maps the failures to exit codes.
- [ ] `dotnet build Foundation/Surl.Networking.UnitTests -warnaserror` is clean, the fast tests are green, and the
      only changed `.cs` lines are comments and the method's name.

## Notes

## Log

- 2026-11-27: Created.
