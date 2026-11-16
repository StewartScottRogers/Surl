---
id: BL-1300
title: Prove pinned upstream curl reads mail over pop3s with SASL EXTERNAL and a client certificate
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1298]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Planning/Decisions/ADR-0049-the-mail-servers-sasl-and-apop-logins.md]
requirement: FR-046
created: 2026-11-16
completed:
---
# BL-1300 — Prove pinned upstream curl reads mail over pop3s with SASL EXTERNAL and a client certificate

## Goal

`Integration` tests show a pinned upstream curl 8.21.0 build presenting a TLS client certificate and logging in to `surl pop3s://` with SASL `EXTERNAL` to retrieve a message, as BL-1298 proved for `smtps`.

## Context

- Same gap, same rules and same build choice as BL-1298 (read its Context and the ADR-0049 amendment it adds): ADR-0049 decision 4 and Amendment 1; `PinnedUpstreamCurl.RunBuildLinkedAgainstAsync(TestContext, "OpenSSL", ...)`; `TestCertificateAuthority` with `ClientCommonName` `alice`; curl forced with `--login-options AUTH=EXTERNAL -u alice:`. POP3 sends the same SASL bytes after `AUTH EXTERNAL` with `+` continuations (ADR-0049, after its table).
- Follow `UpstreamCurlReadsMailFromSurlOverPop3Tests` (it first has curl deliver the mail over SMTP into the same `--directory`, then retrieves message 1); add the tests to that class. Deliver to `alice` with a login the class already uses, then retrieve with `EXTERNAL`.
- Stand-in: reading back over POP3 what was sent over SMTP is the mail store's cross-protocol scenario (ADR-0100); no new store, event or fault.

## Acceptance criteria

- [ ] `UpstreamCurlReadsMailFromSurlOverPop3Tests` has `Retrieve_SaslExternalWithAClientCertificateOverPop3s_Exits0WithTheMessage`: curl exits 0 and standard output is the delivered message's bytes; `[TestCategory("Integration")]`.
- [ ] It has `Retrieve_SaslExternalWithoutAClientCertificate_<measured exit>` (surl `--self-signed`, no `--cacert`; curl `-k`), asserting the exit code curl gave and empty standard output.
- [ ] `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~UpstreamCurlReadsMailFromSurlOverPop3Tests&FullyQualifiedName~SaslExternal"` passes on Windows with the static-curl OpenSSL build installed; `dotnet build Conformance/Surl.Conformance.UnitTests -warnaserror` is clean and the fast run is unchanged.
- [ ] BL-1298's ADR-0049 amendment names these tests and their measured exit codes too.

## Notes

## Log

- 2026-11-16: Created.
