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
completed:
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

- [ ] A named test completes a SCRAM-SHA-512 exchange against an account, with a client proof computed in the test
      from RFC 5802's formulas over SHA-512, and the server signature checked.
- [ ] A named test refuses a wrong password with the same outcome as SCRAM-SHA-256.
- [ ] `GetSaslMechanisms` lists `SCRAM-SHA-512` when `--auth` accepts it, in the order Notes records.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-516 (ADR-0105 decision 5).

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
