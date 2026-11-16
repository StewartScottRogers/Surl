---
id: BL-1278
title: Judge coaps pre-shared-key logins and give the command point an Account
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1244, BL-1275]
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests]
requirement: FR-061
created: 2026-11-16
completed: 2026-11-16
---
# BL-1278 — Judge coaps pre-shared-key logins and give the command point an Account

## Goal

A `coaps` PSK login publishes `LoginJudgedEvent`, honours `refuse-login`, and the command-stage fault point carries the PSK identity as `Account`.

## Context

ADR-0170 decisions 6 to 9. `SecureCoapProtocolServer` supplies the `IDtlsLoginJudge` from BL-1275 out of its `ExchangeContext`. Judged by RFC 6347 and RFC 4279.

## Acceptance criteria

- [x] A PSK handshake that verifies publishes `LoginJudgedEvent` (`Mechanism` `psk`, `Account` the identity, `Scheme` `coaps`, accepted); a wrong key or unknown identity publishes one refused.
- [x] A `refuse-login;account=<identity>` rule publishes `FaultAppliedEvent` then a refused `LoginJudgedEvent` with `IsRefusedByFault`, and the handshake ends in `decrypt_error`; it is asked only after the `Finished` verified.
- [x] `CoapRequestFaults` sets `Account` to the PSK identity for a PSK session and leaves it empty otherwise; a rule scoped by `account` matches.
- [x] The Coap project keeps its quality gates.

## Notes

- `CoapPreSharedKeyLoginJudge` (internal, Coap project) is the `IDtlsLoginJudge`; `SecureCoapProtocolServer` passes it in `DtlsServerOptions.LoginJudge`. The PSK identity reaches `CoapRequestFaults.ApplyAsync` through an optional `account` parameter on `CoapFlowServing.ServeAsync`, so plain `coap` is unchanged. Covered by new `SecureCoapProtocolServerTests`.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. coaps PSK logins judged; Account on the command point
