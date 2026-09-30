---
id: BL-195
title: Check the challenge-response SASL mechanisms and POP3 APOP in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-194]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests]
requirement: FR-046
created: 2026-09-29
completed: 2026-09-29
---
# BL-195 — Check the challenge-response SASL mechanisms and POP3 APOP in Surl.Authentication

## Goal

`AuthenticationPolicy` implements BL-193's contract for the challenge-response mechanisms
BL-185's ADR decides (`CRAM-MD5`, RFC 2195, and `DIGEST-MD5`, RFC 2831, if decided) and for
POP3's `APOP` (RFC 1939 section 7), issuing challenges from injected randomness and checking
the responses against the accounts.

## Context

- Decisions: BL-185's ADR (which mechanisms, challenge formats, realm, failure answers,
  `CheckedLogin` words); ADR-0032 section 8 (`FixedTimeEquals`, the dummy account, the 1-second
  delay). These send a value computed from the secret, so they are not plain-text (ADR-0032
  section 3, as BL-185's ADR confirms).
- HMAC-MD5 and MD5 from the BCL (`HMACMD5`, `MD5`). Challenges and nonces from an injected
  random source (see `RandomNtlmServerChallengeSource.cs`) and `TimeProvider`, so tests are
  deterministic; the `APOP` timestamp is the one the POP3 greeting carried, passed in by the
  server.
- If BL-185's ADR decides no `DIGEST-MD5`, the criteria below that name it do not apply; say so in
  Notes.

## Acceptance criteria

- [x] `APOP` passes RFC 1939 section 7's example (timestamp `<1896.697170952@dbc.mtview.ca.us>`,
      secret `tanstaaf`, digest `c4c9334bac560ecc979e58001b3e22fb`), cited.
- [x] `CRAM-MD5` passes RFC 2195 section 2's example, cited; `DIGEST-MD5`, if decided, passes RFC
      2831 section 4's example, cited.
- [x] Fast tests cover for each: a wrong response, an unknown user and no accounts refused with
      the same verdict after the 1-second delay; `--allow-anonymous`; a malformed response; the
      login note never holding a secret.
- [x] `dotnet build Surl.Authentication.UnitLibrary -warnaserror` is clean; the fast tests pass;
      `Measure-CodeQuality.ps1 -Library Surl.Authentication.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

- ADR-0049 decides `DIGEST-MD5` in (section 3, out of the default set as Historic, RFC 6331), so
  every criterion naming it applies. `CramMd5`, `DigestMd5` and `Apop` join `AuthenticationMethod`;
  only `CramMd5` joins `DefaultAccepted`, as ADR-0049 section 3's table says. The `--auth` words
  themselves are BL-197's, in `Surl.Cli`.
- The challenges' random bytes come from an injected `ISaslNonceSource` (internal constructor on
  `AuthenticationPolicy`), the Unix time from the `TimeProvider`; tests pin the challenge and nonce
  ADR-0049 measured upstream curl 8.21.0 against, and check curl's recorded responses.
- `Md5HexDigest` compares a sent digest in fixed time even when it is malformed, so a short or
  non-hex digest costs what a wrong one does (ADR-0032 section 8). An initial response to
  `CRAM-MD5` or `DIGEST-MD5` is refused as a bad credential, since the server speaks first.
- `--allow-anonymous` on `DIGEST-MD5` still sends an `rspauth` continuation (MD5 over nothing)
  because curl waits for one before its empty answer; curl does not check it.
- Measured 2026-09-29: `Surl.Authentication.UnitLibrary` 100% line, 100% branch, 333 members,
  0 failing, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. CRAM-MD5, DIGEST-MD5 and APOP checked against the RFC examples and upstream curl's measured responses; 100% coverage
