---
id: BL-1275
title: Give Surl.Dtls an IDtlsLoginJudge called once the client Finished verified
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1244]
touches: [Cryptography/Surl.Dtls.UnitLibrary, Cryptography/Surl.Dtls.UnitTests]
requirement: FR-061
created: 2026-11-16
completed:
---
# BL-1275 — Give Surl.Dtls an IDtlsLoginJudge called once the client Finished verified

## Goal

`DtlsServerOptions` takes an optional `IDtlsLoginJudge` that is told each PSK login's verdict and can refuse it as a wrong key.

## Context

ADR-0170 decisions 6 to 8, modelled on ADR-0165's TLS-SRP judge. `Surl.Dtls` stays free of every Surl project. Judged by RFC 6347 and RFC 4279; no upstream curl measurement.

## Acceptance criteria

- [ ] `IDtlsLoginJudge` has `ShouldRefuseAsync(string identity, CancellationToken)` and `Judged(string identity, bool isAccepted)`; the option defaults to none and behaviour is then unchanged.
- [ ] `Judged` is called once per handshake that reaches the client `Finished`, true when it verified, false for an unknown identity or wrong key; never for a handshake that failed earlier.
- [ ] `ShouldRefuseAsync` is asked only after the `Finished` verified; when it returns true the server sends a fatal `decrypt_error`, no `ChangeCipherSpec` and no server `Finished`.
- [ ] Surl.Dtls keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-16: Created.
