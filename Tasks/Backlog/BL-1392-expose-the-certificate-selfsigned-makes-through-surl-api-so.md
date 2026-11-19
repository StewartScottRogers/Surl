---
id: BL-1392
title: Expose the certificate SelfSigned makes through Surl.Api so a test can trust it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-19
completed:
---
# BL-1392 — Expose the certificate SelfSigned makes through Surl.Api so a test can trust it

## Goal

A test that starts a `Surl.Api` server with `SurlServerOptions.SelfSigned = true` can read the certificate the server
made and pin it in its client, so it can keep TLS verification on.

## Context

- Surly cookbook bug report 2026-11-18 20:25, finding 4. Today the API exposes neither the made certificate nor its
  public part, so a test must turn verification off (`curl -k` style) or make its own certificate and pass
  `CertificateFile`/`KeyFile`.
- The certificate is made in `Foundation/Surl.Networking.UnitLibrary/ThrowawayServerCertificate.cs:46`
  (`CreateSelfSigned`); `Application/Surl.Api/SurlServerOptions.cs` and `SurlServerArguments.cs` carry the option
  through `Surl.Composition`.

## Acceptance criteria

- [ ] `SurlServer` gains a read-only member (e.g. `SelfSignedCertificatePem`: the public certificate only, as PEM)
      that is set once the server has started with `SelfSigned` and is `null` otherwise. The private key is never
      exposed.
- [ ] A named test in `Surl.Api.UnitTests` starts an HTTPS server on port 0 with `SelfSigned`, reads the PEM, and
      completes a request with an `HttpClient` (or `SslStream`) that trusts only that certificate, verification on.
- [ ] Two servers side by side each report their own certificate (a named test); no static mutable state.
- [ ] The `Surl.Api` README documents the member with a short example; XML docs written.
- [ ] `dotnet build` clean, fast tests green, coverage gates held.

## Notes

- Consider whether `surl-mcp`'s `start_server` answer should carry the same PEM; if yes, file it as a follow-up task.

## Log

- 2026-11-19: Created.
