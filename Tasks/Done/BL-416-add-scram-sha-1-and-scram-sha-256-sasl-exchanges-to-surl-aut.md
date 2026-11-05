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
completed: 2026-11-04
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

- [x] Named tests replay RFC 5802 section 5's SCRAM-SHA-1 example and RFC 7677 section 3's SCRAM-SHA-256 example
      (user `user`, password `pencil`, the RFC's nonces and salt) and produce the RFC's `server-first-message`
      and `server-final-message` byte for byte.
      (`ScramSaslMechanismTests.Exchange_Rfc5802Section5Example_...`, `Exchange_Rfc7677Section3Example_...`)
- [x] Named tests show:
      - a wrong proof refused with `e=invalid-proof`; (`Exchange_WrongProof_IsRefusedWithInvalidProof`)
      - an unknown user refused without saying whether the user exists (ADR-0006 section 3);
        (`Exchange_UnknownUser_GetsAConsistentSaltAndTheSameRefusalAsAWrongPassword`)
      - a nonce that does not start with the client's nonce refused;
        (`Exchange_NonceThatIsNotTheIssuedOne_IsRefusedWithOtherError`)
      - a malformed message refused. (`Exchange_MalformedClientFirstMessage_...`, `Exchange_MalformedClientFinalMessage_...`)
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Authentication` keeps 100% line and
      branch coverage.

## Notes

Decided by Claude under Stewart's delegation. The ADR follows in BL-546: `Documentation/Planning/Decisions`
was in BL-358's `touches` while this ran, so no ADR was written here.

- Where: `ScramSaslExchange` (one class over `ScramHash.Sha1` / `ScramHash.Sha256`), `ScramClientFirstMessage`,
  `ScramClientFinalMessage`, `ScramCredentials`, `SaslPrep`, `AccountBook.FindScramCredentials`. Reached
  through `ISaslAuthenticationPolicy` as ADR-0084 decision 4 says; no contract changed.
- `AuthenticationMethod` gains `ScramSha256` and `ScramSha1`, offered after GSSAPI and before DIGEST-MD5
  (strongest first). Neither is in `DefaultAccepted`, and `Surl.Console` has no `--auth` word yet (BL-547),
  so no original server's offer or bytes change.
- No `-PLUS` mechanism, so no channel binding: GS2 flag `n` and `y` accepted, `p=` refused
  `channel-binding-not-supported`, `m=` `extensions-not-supported` (RFC 5802 sections 6, 7).
- Server nonce: base64 of 18 bytes from the policy's `ISaslNonceSource`; the exchange's internal
  constructor takes a nonce factory so the RFCs' nonces replay.
- Salt and iterations: per account and per hash, a random 16-byte salt and 4096 iterations, derived when
  the `AccountBook` is built; only `StoredKey` and `ServerKey` are kept.
- Unknown user: a dummy whose salt is HMAC-SHA256 of mechanism and name under a per-book random key
  (the same each time, RFC 5802 section 9), 4096 iterations, random keys; refused `e=invalid-proof` like
  a wrong password.
- Server errors: `SaslLoginStep` has no failure-data field, so a refusal carries its
  `server-final-message` `e=<error>` as `RefusalNote`: `invalid-encoding` (malformed),
  `channel-bindings-dont-match`, `other-error` (nonce not the issued one, or `a=` naming another user),
  `invalid-proof`.
- Success: `v=<ServerSignature>` as a challenge answered empty on the mail servers (as DIGEST-MD5's
  `rspauth`), or as `AdditionalSuccessData` where `CanCarrySecurityLayer`.
- `--allow-anonymous`: every step runs; a well-formed final message is accepted unchecked.
- Normalize: SASLprep by hand, RFC 4013's mapping (C.1.2 spaces to U+0020, B.1 removed) then NFKC with
  `string.Normalize`. The prohibited-output and bidi checks are left out: they only refuse, and a
  conforming client never sends a password they would refuse. Account names match exactly.
- Measured: `Measure-CodeQuality.ps1 -Library Surl.Authentication.UnitLibrary` gives 100% line and
  branch; its one failing member, `GssapiSaslExchange.ServiceOf` (Cobertura complexity 12), predates
  this task. Full `dotnet build -warnaserror` clean, fast tests green (Surl.Authentication.UnitTests: 978 passed, 50 of them new).
- Filed: BL-546 (the ADR), BL-547 (the `--auth` words).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Surl.Authentication answers SASL SCRAM-SHA-1 and SCRAM-SHA-256 (no channel binding), replaying RFC 5802 and RFC 7677 byte for byte
