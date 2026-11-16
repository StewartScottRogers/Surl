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
completed: 2026-11-16
---
# BL-1275 — Give Surl.Dtls an IDtlsLoginJudge called once the client Finished verified

## Goal

`DtlsServerOptions` takes an optional `IDtlsLoginJudge` that is told each PSK login's verdict and can refuse it as a wrong key.

## Context

ADR-0170 decisions 6 to 8, modelled on ADR-0165's TLS-SRP judge. `Surl.Dtls` stays free of every Surl project. Judged by RFC 6347 and RFC 4279; no upstream curl measurement.

## Acceptance criteria

- [x] `IDtlsLoginJudge` has `ShouldRefuseAsync(string identity, CancellationToken)` and `Judged(string identity, bool isAccepted)`; the option defaults to none and behaviour is then unchanged.
- [x] `Judged` is called once per handshake that reaches the client `Finished`, true when it verified, false for an unknown identity or wrong key; never for a handshake that failed earlier.
- [x] `ShouldRefuseAsync` is asked only after the `Finished` verified; when it returns true the server sends a fatal `decrypt_error`, no `ChangeCipherSpec` and no server `Finished`.
- [x] Surl.Dtls keeps 100% line and branch coverage.

## Notes

- Wrong key shows as an epoch 1 tag failure before AcceptFinished, so both paths call Judged(false) for a PSK login (identity known). Judge is a library-local interface; no ADR needed beyond ADR-0170.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. Judge added, tests green
