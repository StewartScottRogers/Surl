---
id: BL-415
title: Add the pre-shared-key mode CoAP mandates and DTLS alerts and closure to Surl.Dtls
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-414]
touches: [Cryptography/Surl.Dtls.UnitLibrary, Cryptography/Surl.Dtls.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-415 — Add the pre-shared-key mode CoAP mandates and DTLS alerts and closure to Surl.Dtls

## Goal

A `Surl.Dtls` server session also completes a pre-shared-key handshake under `TLS_PSK_WITH_AES_128_CCM_8`
(RFC 7252 section 9.1.3.1). It looks the identity up through a caller-given lookup, sends and honours
`close_notify`, and sends the alerts the ADR names for each failure.

## Context

- Decision: BL-403's ADR (PSK configuration, the alert per failure). RawPublicKey (RFC 7250) is built here
  too if the ADR chose it.
- RFC 4279 sections 2 and 5 (PSK key exchange, the premaster secret, `unknown_psk_identity`), RFC 6655 (the
  CCM_8 suites), RFC 6347 section 4.1.2.7 and RFC 5246 section 7.2 (alerts, `close_notify`).
- The identity lookup is a delegate or interface the caller supplies (the CoAP server wires it to the accounts
  file). This library knows no accounts.

## Acceptance criteria

- [ ] Named tests show:
      - a PSK handshake completed and application data exchanged;
      - an unknown identity ending with `unknown_psk_identity`;
      - a wrong key ending with `decrypt_error` or the ADR's alert;
      - `close_notify` sent on close and honoured on receipt;
      - every alert the ADR lists sent for its failure.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
