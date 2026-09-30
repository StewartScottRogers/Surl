---
id: BL-218
title: Check SASL GSSAPI logins in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-217, BL-193, BL-197, BL-240]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests]
requirement: FR-046
created: 2026-09-29
completed:
---
# BL-218 — Check SASL GSSAPI logins in Surl.Authentication

## Goal

`AuthenticationPolicy` runs RFC 4752 SASL `GSSAPI` through `IMailAuthenticationPolicy`, checking
the client's Kerberos AP-REQ the way BL-217's ADR decides. When `gssapi` is accepted, `GSSAPI` is
offered first in the mechanism list (ADR-0049 decision 2). `--auth gssapi` stops being refused as
"not available in this build", and it stays out of the default set (ADR-0049 decision 3).

## Context

- Decision: `Documentation/Planning/Decisions/ADR-0049-the-mail-servers-sasl-and-apop-logins.md`
  - decision 4: `GSSAPI` is Kerberos V5, built by hand; curl 8.21.0 (Windows reference build,
    pinned in `UpstreamCurlBuilds.json`) picks it unasked only for a user name holding a realm,
    so a server that offers it must be able to finish it.
  - decision 2: `GSSAPI` is first in the offered list
    (`GSSAPI`, `DIGEST-MD5`, `CRAM-MD5`, `NTLM`, `OAUTHBEARER`, `XOAUTH2`, `PLAIN`, `LOGIN`,
    `EXTERNAL`), offered only when `gssapi` is accepted and, as BL-217's ADR decides, a service
    key is configured.
  - decision 3: `gssapi` sits after `negotiate` and before `ntlm` in the `--auth` table and the
    warning order; it is not in the default set.
  - decision 5: randomness from an injected source and time from the injected `TimeProvider`;
    every secret comparison is `CryptographicOperations.FixedTimeEquals`.
  - decision 6: the contract (`IMailAuthenticationPolicy`, `ISaslExchange`, `MailLoginStep`,
    `MailLoginOutcome`), added by BL-193.
  - decision 7: `RefusedCredentials` after the 1-second delay on the injected `TimeProvider`;
    the `CheckedLogin` method is `GSSAPI`.
  - decision 8: ADR-0049 calls this task "BL-211" and the Kerberos decision "BL-210". Those IDs
    went to other tasks when these were filed, so ADR-0049's BL-211 means this task and its
    BL-210 means BL-217.
- BL-217's ADR (under `Documentation/Planning/Decisions/`, indexed in its `README.md`) decides
  the Kerberos library, the service key, the enctypes, the replay cache, clock skew, the test
  vectors and the command-line options. Read it first and use the library it names; do not
  re-decide any of it here. If that ADR files a Kerberos library task, this task's
  `depends-on` should already include it (BL-217 updates this file).
- BL-217's ADR is
  `Documentation/Planning/Decisions/ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md`.
  Its decision 9 decides the RFC 4752 `GSSAPI` exchange (initial token, AP-REP only when
  `mutual-required` with an empty client answer, the wrapped 4-byte offer `01 00 00 00`, the
  client's wrapped choice and authorization identity) and its decision 10 the account (the ticket's
  client principal in RFC 1964 display form, e.g. `user@EXAMPLE.COM`; `CheckedLogin` method
  `GSSAPI`). The check itself is `Surl.Kerberos`'s `KerberosAcceptor` and
  `KerberosSecurityContext` (ADR-0057 decision 6; built by BL-243 and BL-239), with the service
  word `smtp`, `imap` or `pop` (decision 2), reached through the Kerberos acceptor BL-240 composes
  onto `AuthenticationSettings`. BL-240 also adds the `--auth gssapi needs --keytab` refusal; this
  task removes only the "not available in this build" one.
- RFC 4752: the client's first response is a GSS-API initial context token (AP-REQ); the server
  answers with the AP-REP token when mutual authentication is requested, then an empty-response
  round, then a wrapped 4-byte security-layer offer (no layer, and the maximum buffer size);
  the client answers with a wrapped choice and the authorization identity, which must be empty
  or equal to the ticket's client principal as BL-217's ADR maps it to an account.
- Code: `Surl.Authentication.UnitLibrary/AuthenticationPolicy.cs` (the mail offer and the
  exchanges, from BL-194..BL-196); `Surl.Cli.UnitLibrary/OptionArgumentReader.cs`,
  `SurlCommandLine.cs`, `CommandLineOptions.cs`, `AiHelpProse.cs`;
  `Surl.Console/AuthenticationComposition.cs`. BL-197 adds the `gssapi` word and its "not
  available in this build" refusal; this task removes that refusal.

## Acceptance criteria

- [ ] A fast test in `Surl.Authentication.UnitTests` replays an AP-REQ built from the test
      vectors BL-217's ADR names, with a fixed service key, then the RFC 4752 security-layer
      exchange, and the login is `Accepted` with `AccountName` the matching account.
- [ ] Fast tests show that a ticket encrypted under the wrong key, an expired ticket (time
      advanced on the injected `TimeProvider` past the ADR's clock skew) and a replayed
      authenticator are each `RefusedCredentials`, decided only after the 1-second delay has been
      advanced, and that an authorization identity other than empty or the ticket's account is
      refused.
- [ ] A fast test shows the note: the deciding step carries a `CheckedLogin` whose method is
      `GSSAPI` and whose user is the name BL-217's ADR says, on both `Accepted` and
      `RefusedCredentials`.
- [ ] A fast test shows `GetMailLoginOffer` lists `GSSAPI` first when `gssapi` is accepted, and
      not at all when it is not accepted; starting `GSSAPI` then gives `RefusedMechanism`
      without the delay.
- [ ] `CommandLineParserTests` pin that `--auth gssapi` is accepted and that the default set
      does not contain `gssapi`; `CommandLineRunnerAuthenticationTests` pin the warning line with
      `gssapi` after `negotiate` and before `ntlm`; `HelpTextTests`, `ManualTextTests`,
      `AiHelpFactsTests` and `AiHelpTextTests` pass with the updated `--auth` explanation.
- [ ] `dotnet build Surl.Authentication.UnitLibrary -warnaserror`,
      `dotnet build Surl.Cli.UnitLibrary -warnaserror` and `dotnet build Surl.Console -warnaserror`
      are clean; `dotnet test --filter "TestCategory!=Integration"` passes; no test needs
      `TestCategory=Integration` or a KDC; every test is platform-neutral.
- [ ] `Measure-CodeQuality.ps1` reports 100% line and 100% branch coverage and no failing member
      for `Surl.Authentication.UnitLibrary`, `Surl.Cli.UnitLibrary` and `Surl.Console`.

## Notes

- If BL-217's ADR puts the Kerberos checking in a new library, this task touches that library
  only as a reference, not by changing it; changes to it belong to the library's own task.

## Log

- 2026-09-29: Created.
- 2026-09-29: Filed by BL-185 (ADR-0049 decision 8).
- 2026-09-30: depends-on and Context updated by BL-217 (ADR-0057).
- 2026-09-30: Backlog -> Doing.
