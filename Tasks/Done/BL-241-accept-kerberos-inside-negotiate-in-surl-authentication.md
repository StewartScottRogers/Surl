---
id: BL-241
title: Accept Kerberos inside Negotiate in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-240]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Documentation/Planning/Decisions/ADR-0063-kerberos-inside-negotiate-the-choices-adr-0057-decision-8-left-open.md, Documentation/Planning/Decisions/README.md]
requirement: FR-046
created: 2026-09-30
completed: 2026-09-30
---
# BL-241 — Accept Kerberos inside Negotiate in Surl.Authentication

## Goal

With `--keytab` given and `negotiate` accepted, `NegotiateAuthenticationMethod` accepts a
Kerberos AP-REQ, bare or inside SPNEGO, logs the client principal in as the matching account, and
answers with the SPNEGO or bare AP-REP token ADR-0057 decision 8 lays out; without `--keytab` it
behaves exactly as ADR-0040 decides.

## Context

- Decision: `Documentation/Planning/Decisions/ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md`.
  - decision 8 (amends ADR-0040 decision 3):
    - SPNEGO: the first `NegTokenInit` `mechTypes` entry surl supports is selected - Kerberos
      (`1.2.840.48018.1.2.2` or `1.2.840.113554.1.2.2`) or NTLM. When Kerberos is selected the
      optimistic `mechToken` must be its AP-REQ; a `NegTokenInit` naming Kerberos first with no
      optimistic token is refused. No fallback to NTLM after a refused ticket.
    - The reply echoes the OID exactly as the client listed it:
      `negTokenResp { negState accept-completed, supportedMech <oid>, responseToken <AP-REP token
      when mutual-required> }`, sent on the accepted response as
      `WWW-Authenticate: Negotiate <base64>` (RFC 4559 section 5; ADR-0032 section 6's final
      token).
    - `mechListMIC`: when the client's `NegTokenInit` carries one, it is checked with
      `KerberosSecurityContext.VerifyMic` over the DER of `mechTypes` (key usage 25), a failure
      refuses the login, and surl sends its own MIC over the same bytes (`GetMic`, key usage 23).
      When the client sends none, surl sends none.
    - A bare Kerberos token (`[APPLICATION 0]` with OID `1.2.840.113554.1.2.2`, not SPNEGO) is
      accepted, answered with the bare AP-REP token when mutual authentication was asked, else
      with no final token.
    - One leg; the connection remembers the login as ADR-0044 decides for Negotiate. A refused
      AP-REQ is `401` with every challenge again, after ADR-0032 section 8's 1-second delay on the
      injected `TimeProvider`.
  - decision 2: the service word for HTTP is `HTTP` (for `http` and `https`).
  - decision 10: the login is the ticket's client principal in `KerberosPrincipalName`'s display
    form (e.g. `user@EXAMPLE.COM`), accepted when an account of exactly that name exists
    (ordinal); its password is not used. The `CheckedLogin` (ADR-0038) has method `Negotiate` and
    user the display form, on `Accepted` and on `RefusedCredentials` after the ticket decrypted; a
    token that never decrypted names no user (ADR-0038 section 6). Under `--allow-anonymous` the
    ticket must still decrypt; only the account match is skipped.
- Code: `Surl.Authentication.UnitLibrary/NegotiateAuthenticationMethod.cs`,
  `NegotiateConnectionVerifier.cs`, `SpnegoNegState.cs`; the Kerberos acceptor arrives on
  `AuthenticationSettings` from BL-240 (null without `--keytab`). The acceptor and context are
  `KerberosAcceptor` and `KerberosSecurityContext` from `Surl.Kerberos.UnitLibrary` (BL-239);
  this task does not change that library.
- Tests: `Surl.Authentication.UnitTests/NegotiateAuthenticationMethodTests.cs` and
  `SpnegoTestTokens.cs` hold the ADR-0040 SPNEGO fixtures; extend them with hand-built Kerberos
  AP-REQs (fixed service key, session key and confounder, as in BL-239's tests) wrapped as MS-SPNG
  lays them out (ADR-0057 decision 11).
- `Surl.Cli.UnitLibrary/AiHelpProse.cs` and `ManualText.cs`: BL-240 wrote that no login uses the
  keytab's keys yet; this task rewrites that sentence to say `negotiate` accepts Kerberos with
  `--keytab` (the `gssapi` sentence stays for BL-218).

## Acceptance criteria

- [x] `NegotiateAuthenticationMethodTests` (or a new `NegotiateKerberosTests` beside it) pin: a
      SPNEGO `NegTokenInit` listing `1.2.840.48018.1.2.2` first with a valid AP-REQ is `Accepted`
      as the matching account, and the final `WWW-Authenticate: Negotiate` token decodes to
      `negState accept-completed` with `supportedMech` `1.2.840.48018.1.2.2`; the same with
      `1.2.840.113554.1.2.2` echoes that OID.
- [x] Tests pin: with `mutual-required` the `negTokenResp` carries the AP-REP token as
      `responseToken`, without it no `responseToken`; a bare Kerberos token is accepted and
      answered with the bare AP-REP token only when `mutual-required` was set.
- [x] Tests pin `mechListMIC`: a valid client MIC is accepted and surl's reply carries a MIC over
      the same `mechTypes` DER that verifies under key usage 23; a tampered client MIC is
      `RefusedCredentials`; no client MIC means no MIC in the reply.
- [x] Tests pin refusals, each `RefusedCredentials` decided only after the 1-second delay is
      advanced on the fake `TimeProvider`, answered `401` with every challenge again and with no
      NTLM fallback: a Kerberos-first `NegTokenInit` without an optimistic token, a ticket under
      the wrong key, an expired ticket, a replayed authenticator, and a valid ticket for a principal
      with no account.
- [x] Tests pin the `CheckedLogin`: method `Negotiate`, user `user@EXAMPLE.COM` on `Accepted` and
      on the no-account refusal; no user for a token that never decrypted; under
      `--allow-anonymous` a valid ticket with no account is accepted unchecked and a wrong-key
      ticket is still refused.
- [x] Without a Kerberos acceptor on `AuthenticationSettings`, every existing ADR-0040 test in
      `NegotiateAuthenticationMethodTests` passes unchanged and a Kerberos token is refused as
      ADR-0040 decision 3 says.
- [x] `AiHelpProse.cs` and `ManualText.cs` state that `negotiate` accepts Kerberos with
      `--keytab`; `AiHelpTextTests`, `ManualTextTests` and
      `CommandLineRunnerAiHelpTests.RunAsync_EveryAiHelpExample_WritesWhatTheExampleShows` pass.
- [x] `dotnet build Surl.Authentication.UnitLibrary -warnaserror` and
      `dotnet build Surl.Cli.UnitLibrary -warnaserror` are clean;
      `dotnet test --filter "TestCategory!=Integration"` passes; no test needs
      `TestCategory=Integration` or a KDC; every test is platform-neutral.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100% branch
      coverage, no method over complexity 10 and no CRAP score over 30 for
      `Surl.Authentication.UnitLibrary`.

## Notes

- No pinned upstream curl build sends a Kerberos token on the lane machine (ADR-0057, "What
  upstream curl 8.21.0 does (measured)"); the bytes here come from the RFCs and MS-SPNG. The
  end-to-end proof is BL-242.
- SASL `GSSAPI` is BL-218, not this task.
- Built (2026-09-30, lane 2): `NegotiateKerberosLogin` answers the Kerberos leg;
  `NegotiateConnectionVerifier` hands it every non-SPNEGO `InitialContextToken` and every
  `NegTokenInit` whose first supported mechanism is Kerberos; `SpnegoToken` now reads the
  `mechTypes` DER and the client `mechListMIC` and writes surl's; `HttpCredentialOutcome` gains
  `AcceptedUnchecked`. Tests: `NegotiateKerberosTests` (29), every ADR-0040 test unchanged.
- Touches widened: `Surl.Console` (one line in `AuthenticationComposition` hands `--keytab`'s
  acceptor and `--allow-anonymous` to the new constructor
  `NegotiateAuthenticationMethod(accounts, kerberosAcceptor, allowAnonymous)`; no task in Doing
  names it), and the new ADR-0063 with its row in the Decisions README (no task in Doing names
  them).
- Decisions (ADR-0063, decided by Claude under Stewart's delegation): Kerberos selected but not
  first in `mechTypes` is refused (one leg, no NTLM fallback); under `--allow-anonymous` with
  `--keytab`, `HttpAuthenticationSession` now reads a Negotiate `Authorization`: a Kerberos ticket
  must decrypt and is served unchecked with its final token, every other Negotiate token is served
  unchecked as before. The acceptance criterion's `--allow-anonymous` case needed this, since
  ADR-0032 otherwise never reads `Authorization` under `--allow-anonymous`.
- A new public constructor rather than one taking `AuthenticationSettings`: an
  `(AuthenticationSettings)` overload made the existing `new NegotiateAuthenticationMethod(null!)`
  test ambiguous, and the criteria keep every ADR-0040 test unchanged.
- The HTTP Kerberos refusal reason is not logged at the verbose level (as BL-260 files for SASL).
- `dotnet format --verify-no-changes` reports only ENDOFLINE on this checkout, also for files this
  task never touched (working-copy line endings); nothing else.

## Log

- 2026-09-30: Created.
- 2026-09-30: Filed by BL-217 (ADR-0057 decision 12).
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Negotiate accepts Kerberos with --keytab: bare or SPNEGO AP-REQ, AP-REP and mechListMIC in the final token, no-account and bad-ticket refusals after the delay
