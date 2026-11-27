---
id: BL-1881
title: Correct the server TLS plus scheme list doc comments in Surl.Composition and TlsSchemes
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1880, BL-1868, BL-1863]
touches: [Application/Surl.Composition.UnitLibrary/ServerTlsComposition.cs, Application/Surl.Composition.UnitLibrary/SurlServerCompositionSeams.cs, Application/Surl.Composition.UnitLibrary/SurlServerComposition.Start.cs, Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs, Foundation/Surl.Protocol.Abstractions.UnitLibrary/TlsSchemes.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1881 — Correct the server TLS plus scheme list doc comments in Surl.Composition and TlsSchemes

## Goal

The doc comments on server TLS, the extended server list and the TLS-from-the-first-byte schemes describe the code as it is.

## Context

Found while working BL-1867 (2026-11-27), read from source. Comment text only; no behaviour change and no reordering of code. Line numbers are as of that date. BL-1880 changes `ServerTlsComposition.cs` first; describe the code as it is when this task runs.

- `Application/Surl.Composition.UnitLibrary/SurlServerCompositionSeams.cs`, `<param name="CreateListenerFactory">` (about lines 14-16): says the TLS settings are `null` when no listen URL is TLS from the first byte. They are `null` only when no listen URL needs TLS (`ServerTlsComposition.NeedsTls`: it requires a certificate, it can upgrade and an upgrade is available, or it is `coaps` with a certificate). This file's `ReadStartFile` comment is BL-1868's.
- `ServerTlsComposition.cs`: the class summary (about lines 11-17), the `Settings` summary (about lines 61-64) and the `Compose` summary (about lines 150-155) give the same incomplete rule, and the class summary leaves out TLS-SRP (`ServerTlsSettings.ForTlsSrp`). The `FindListenUrlWithoutCertificate` summary (about lines 115-119) names `natss` and `xmpp` but not `webtransport`, `coaps` without an account, or the TLS-SRP exception.
- `SurlServerComposition.Start.cs`, the class summary (about lines 24-28): lists "the content store, the listen-URL refusals", but `ComposeAvailableAsync` builds only the content file system before the listen-URL refusals; the content store is built after authentication, the shares and the message bus configuration.
- `ExtendedProtocolServers.cs`, the `Registrations` summary (about lines 61-66): its "today ..." list omits registered servers (among them `amqp`, `az`, `gs`, `kafka`, `mcp`, `mcp+stdio`, `mqtt5`, `oci`, `redis` and `signalr`). It also says the entries are in ordinal order of their first scheme, while the `gs` entry precedes the `grpc` one.
- `Foundation/Surl.Protocol.Abstractions.UnitLibrary/TlsSchemes.cs`: the `<returns>` list of `IsImplicitTls` says "exactly" but omits `oci`, `rediss`, `signalrs` and `mcps`, which `ImplicitTlsSchemes` holds; the class summary names only some of the extended entries.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [x] The `CreateListenerFactory`, `ServerTlsComposition` class, `Settings` and `Compose` comments say the settings are absent only when no listen URL needs TLS, agree with `NeedsTls`, and the class summary mentions TLS-SRP.
- [x] The `FindListenUrlWithoutCertificate` summary agrees with `RequiresCertificate` and `IsSecuredByTlsSrp`.
- [x] The `SurlServerComposition` class summary in `SurlServerComposition.Start.cs` gives the start's steps in the order `ComposeAsync` and the methods it calls run them.
- [x] The `Registrations` summary either names every server in the list or names none and points to the list, and says nothing about ordering that the list does not keep.
- [x] The `TlsSchemes.IsImplicitTls` `<returns>` text and the class summary either name exactly the schemes in `ImplicitTlsSchemes` or point to that set without a partial list.
- [x] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean and the diff of each `.cs` file changes comment lines only.

## Notes

- 2026-11-27: `Registrations` and both `TlsSchemes` texts now point to the list or set and name no partial list, so
  they cannot fall behind when a server or scheme is added.
- 2026-11-27: Corrected the same kind of false statement elsewhere in the five touched files too (the `ComposeAsync`,
  `FormatMissingCertificate`, `IsCertificateConfigured` and upgradable-scheme comments, the `Registrations` remarks).
- 2026-11-27: Verified: build with warnings as errors clean, format check clean, fast tests 30310 passed, 0 failed,
  3 skipped; 0 non-comment lines in the `.cs` diff. Integration tests not run: comments only.
- 2026-11-27: Filed BL-1920, BL-1921 and BL-1922 for false statements found outside `touches`.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. Server TLS, ordered start, extended registration and implicit TLS scheme doc comments now describe the code as it is
