---
id: BL-1131
title: Compose the https-mtls server half in the upstream test-case run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1130]
touches: [Conformance/Surl.Conformance.UnitTests]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1131 — Compose the https-mtls server half in the upstream test-case run

## Goal

The upstream test-case run composes the `https-mtls` server half on `%HTTPS-MTLSPORT`: the HTTP
half behind TLS with the `test-localhost` certificate, requiring a client certificate issued by the
run's generated `test-ca`. Upstream tests 2088 and 2089 then run on CI's Linux and macOS legs
instead of skipping `server-half-not-composed:https-mtls`, and pass.

## Context

- CI run 43629075978: 2088 and 2089 are the only `server-half-not-composed` skips left (2 on each of
  Linux and macOS). On Windows they skip `feature-missing:!Schannel`, so the lane cannot run them.
- ADR-0080 decision 2's table: `https-mtls` is "as `https`, requiring a client certificate issued by
  the generated `test-ca` (ADR-0010)", verbatim reply. `UpstreamTestCaseServers` already marks it
  built, `UpstreamTestCaseServerCommands` maps it to the `Http` set and `HTTPS-MTLSPORT` is in
  `UpstreamTestCaseVariables`; what is missing is in `Conformance/Surl.Conformance.UnitTests`:
  - `ServerFamilies` in `UpstreamTestCasesAgainstSurlTests.cs` has no `https-mtls` entry, so the case
    is written `server-half-not-composed`;
  - `UpstreamTestCaseServerHalves` has no listener for it and `RunEnvironment` no `HTTPS-MTLSPORT`.
- `ServerTlsSettings(certificate, intermediates, clientTrustAnchors, timeProvider)` in
  `Foundation/Surl.Networking.UnitLibrary` takes the client trust anchors; pass the generated
  `test-ca` certificate. BL-1130 (which this depends on) makes the run's TLS settings per
  certificate; build on that.
- 2088 sends `--cert test-client-eku-only.crt --key test-client-eku-only.key` with
  `--cacert test-ca.crt`; 2089 sends `test-client-cert` with `--insecure`. Both expect the plain GET
  in `<protocol>`. `UpstreamTestCertificateGenerator` already writes both client certificates.
- Stand-in: not applicable. This is the test project's composition.

## Acceptance criteria

- [ ] `UpstreamTestCaseServerHalves` binds an `https-mtls` listener that requires a client
      certificate chaining to the run's `test-ca`, and `RunEnvironment` gives `HTTPS-MTLSPORT` its
      port.
- [ ] A fast test selects 2088 and 2089 under the `linux-x64` reference pin's features with no skip
      reason, and under the `win-x64` reference pin with `feature-missing:!Schannel`.
- [ ] A local Windows run of `UpstreamTestCasesAgainstSurlTests` writes no
      `server-half-not-composed` line and still passes every case on
      `Documentation/Conformance/upstream-test-cases-passing.win-x64.txt`.
- [ ] `dotnet build Conformance/Surl.Conformance.UnitTests -warnaserror` is clean and the fast tests
      are green.

## Notes

## Log

- 2026-11-14: Created.
