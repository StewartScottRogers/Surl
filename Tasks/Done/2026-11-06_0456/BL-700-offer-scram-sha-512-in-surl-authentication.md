---
id: BL-700
title: Offer SCRAM-SHA-512 in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-516]
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-700 — Offer SCRAM-SHA-512 in Surl.Authentication

## Goal

`Surl.Authentication`'s SASL mechanisms include `SCRAM-SHA-512`, run by the existing `ScramSaslExchange` with a
SHA-512 `ScramHash`, so the Kafka broker can offer it as ADR-0105 decision 5 says.

## Context

- Decision: ADR-0105 decision 5; ADR-0100's account table already names SCRAM-SHA-512 for Kafka.
- `Foundation/Surl.Authentication.UnitLibrary/SaslMechanism.cs` lists `SCRAM-SHA-256` and `SCRAM-SHA-1`;
  `ScramHash` and `ScramSaslExchange` hold the RFC 5802 exchange. SHA-512 and HMAC-SHA-512 are in the BCL
  (`SHA512`, `HMACSHA512`, `Rfc2898DeriveBytes.Pbkdf2` with `HashAlgorithmName.SHA512`).
- The name is the IANA SASL registry's `SCRAM-SHA-512`. Decide whether it needs its own `AuthenticationMethod`
  and `--auth` name, as `ScramSha256` has, and where it sorts in the offer order (after `SCRAM-SHA-256`); record
  the choice in Notes. Which servers offer it is each server's filter on the policy's list.

## Acceptance criteria

- [x] A named test completes a SCRAM-SHA-512 exchange against an account, with a client proof computed in the test
      from RFC 5802's formulas over SHA-512, and the server signature checked.
- [x] A named test refuses a wrong password with the same outcome as SCRAM-SHA-256.
- [x] `GetSaslMechanisms` lists `SCRAM-SHA-512` when `--auth` accepts it, in the order Notes records.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-516 (ADR-0105 decision 5).
- Decision: `SCRAM-SHA-512` gets its own `AuthenticationMethod.ScramSha512`, as `ScramSha256` and `ScramSha1`
  have, so `--auth` can turn it on and off by itself like every other SASL mechanism. Declared and offered after
  `SCRAM-SHA-256` and before `SCRAM-SHA-1`: GSSAPI, SCRAM-SHA-256, SCRAM-SHA-512, SCRAM-SHA-1, DIGEST-MD5, ...
  SCRAM-SHA-256 stays the first SCRAM offered (the standards-track one, RFC 7677; a client taking the first
  offered keeps today's choice), and SHA-1 stays the weakest, last. Not in the default set, as the other SCRAMs
  are not. Recorded in the library's CLAUDE.md; ADR-0125's amendment goes with the `--auth` word in BL-915,
  since `Documentation/Planning/Decisions` was held by BL-687.
- The `--auth` word `scram-sha-512` lives in `Surl.Cli` and `Surl.Composition`, outside this task's `touches`
  (and held by BL-548 and BL-803): filed as BL-915. Until it lands no server offers SCRAM-SHA-512.
- Every named account now also derives SHA-512 SCRAM credentials (PBKDF2, 4096 iterations) at start-up.
- Tests: `ScramSaslMechanismTests.Exchange_ScramSha512_AcceptsAProofComputedOverSha512AndSendsTheServerSignature`,
  `Exchange_ScramSha512WrongPassword_IsRefusedWithInvalidProofAsScramSha256Is`,
  `StartSaslExchange_ScramSha512WithRandomSalt_AcceptsTheAccountsPassword`,
  `GetSaslMechanisms_ScramSha512_IsListedOnlyWhenAcceptedAfterScramSha256`,
  `StartSaslExchange_ScramSha512NotAccepted_IsRefusedAsAnUnknownMechanism`; the client proof and server
  signature are computed with the BCL's `HMACSHA512`/`SHA512`/`Pbkdf2`, not `ScramHash`.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. SCRAM-SHA-512 runs in Surl.Authentication, offered after SCRAM-SHA-256 when --auth accepts it; --auth word filed as BL-915
