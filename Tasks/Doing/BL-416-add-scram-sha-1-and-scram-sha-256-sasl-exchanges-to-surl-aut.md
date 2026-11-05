---
id: BL-416
title: Add SCRAM-SHA-1 and SCRAM-SHA-256 SASL exchanges to Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-401]
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-416 — Add SCRAM-SHA-1 and SCRAM-SHA-256 SASL exchanges to Surl.Authentication

## Goal

`Surl.Authentication` answers the SASL mechanisms SCRAM-SHA-1 (RFC 5802) and SCRAM-SHA-256 (RFC 7677), without
channel binding, as a server. Any server that offers SASL through `ISaslAuthenticationPolicy` (first the XMPP
server) can offer them for accounts in the accounts file.

## Context

- Decision: BL-401's ADR (SCRAM lives in `Surl.Authentication`, reached through the existing contracts).
- Model: the existing exchanges, e.g. `CramMd5SaslExchange`, `DigestMd5SaslExchange`, `PlainSaslExchange`, with
  `ISaslNonceSource` / `RandomSaslNonceSource` for the server nonce and `CryptographicSecretComparer` for
  constant-time comparison.
- RFC 5802 section 5 (the messages), section 3 (`SaltedPassword := Hi(Normalize(password), salt, i)`,
  ClientKey, StoredKey, ServerSignature), and section 7 (formal syntax; `e=` server errors). RFC 7677 adds
  SHA-256. Normalize is SASLprep (RFC 4013). Decide in the task whether ASCII passwords only are accepted, or
  SASLprep is built by hand with the BCL's `string.Normalize(NormalizationForm.FormKC)` plus the RFC 4013
  tables, and record the choice in the class's doc comment.
- No `-PLUS` variant is offered, so Surl supports no channel binding. A GS2 header of `p=` is refused with
  `e=channel-binding-not-supported`, and `n` and `y` are accepted (RFC 5802 sections 6 and 7).
- Iteration count and salt: per account, derived when the accounts file is loaded. Use 4096 iterations, the
  RFC's example minimum, unless the ADR says otherwise.

## Acceptance criteria

- [ ] Named tests replay RFC 5802 section 5's SCRAM-SHA-1 example and RFC 7677 section 3's SCRAM-SHA-256 example
      (user `user`, password `pencil`, the RFC's nonces and salt) and produce the RFC's `server-first-message`
      and `server-final-message` byte for byte.
- [ ] Named tests show:
      - a wrong proof refused with `e=invalid-proof`;
      - an unknown user refused without saying whether the user exists (ADR-0006 section 3);
      - a nonce that does not start with the client's nonce refused;
      - a malformed message refused.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Authentication` keeps 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
