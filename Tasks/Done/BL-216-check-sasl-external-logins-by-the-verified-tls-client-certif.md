---
id: BL-216
title: Check SASL EXTERNAL logins by the verified TLS client certificate in Surl.Authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-193, BL-194, BL-197]
touches: [Surl.Authentication.UnitLibrary, Surl.Authentication.UnitTests, Surl.Cli.UnitLibrary, Surl.Cli.UnitTests, Surl.Console, Surl.Console.UnitTests]
requirement: FR-046
created: 2026-09-29
completed: 2026-09-29
---
# BL-216 — Check SASL EXTERNAL logins by the verified TLS client certificate in Surl.Authentication

## Goal

`AuthenticationPolicy` runs SASL `EXTERNAL` through `IMailAuthenticationPolicy` and logs the
client in as its verified TLS client certificate, exactly as ADR-0049 decision 4 says, and
`--auth external` stops being refused as "not available in this build" and joins the `--auth`
default set (ADR-0049 decision 3), with `--help`, the manual and `--aihelp` saying so.

## Context

- Decision: `Documentation/Planning/Decisions/ADR-0049-the-mail-servers-sasl-and-apop-logins.md`
  - decision 4 (`EXTERNAL`): the identity is the certificate's subject simple name,
    `X509Certificate2.GetNameInfo(X509NameType.SimpleName, false)`; the login is accepted when an
    account of exactly that name exists (its password is not used) and the authorization identity
    the client sends (curl sends the `-u` user name, measured with curl 8.21.0, Windows reference
    build) is empty or equal to it, compared ordinally; otherwise `RefusedCredentials`. Offered
    only when `TlsSession.ClientCertificate` is not `null`; not a plain-text mechanism.
  - decision 2: `EXTERNAL` is last in the offered mechanism list, and only on a connection
    whose `TlsSession.ClientCertificate` is not `null`.
  - decision 3: the `--auth` table, where `external` sits after `xoauth2` and before
    `aws-sigv4`; once this task lands the default set is
    `digest,cram-md5,basic,plain,login,bearer,oauthbearer,xoauth2,external,aws-sigv4`, which is
    also the order of the warning `surl: warning: --auth: accepted methods are <methods>` and of
    `--help`'s list.
  - decision 5 (`EXTERNAL`'s exchange): without an initial response, one empty challenge; the
    response is the authorization identity, possibly empty. Under `--allow-anonymous` the first
    response is accepted as `AcceptedUnchecked` with nothing checked.
  - decision 6 (the contract `IMailAuthenticationPolicy`, `ISaslExchange`, `MailLoginStep`,
    `MailLoginOutcome`, added by BL-193); decision 7 (the 1-second delay on the injected
    `TimeProvider` before `RefusedCredentials`, no delay for `RefusedMechanism`; the
    `CheckedLogin` method is `EXTERNAL` and the user the certificate's name).
  - decision 8: this is the `EXTERNAL` task that ADR-0049 calls "BL-209". That ID went to
    another task when this one was filed, so wherever ADR-0049 says BL-209 for `EXTERNAL`, it
    means this task.
- ADR-0010 section 5: `--cacert` makes every TLS handshake require a client certificate that
  chains to its anchors, so `TlsSession.ClientCertificate` is already verified when it reaches
  the policy (`Surl.Protocol.Abstractions.UnitLibrary/TlsSession.cs`).
- Code where the work lands: `Surl.Authentication.UnitLibrary/AuthenticationPolicy.cs` (it
  implements `IMailAuthenticationPolicy` once BL-194 lands, with BL-194's offer and exchange
  code), `AccountBook.cs` (account lookup); `Surl.Cli.UnitLibrary/OptionArgumentReader.cs` (the
  `--auth` words), `SurlCommandLine.cs` (`DefaultAuthenticationMethods`), `CommandLineOptions.cs`
  (`--auth`'s `Default` and explanation), `AiHelpProse.cs`; `Surl.Console/AuthenticationComposition.cs`
  (word to method map and the accepted-methods warning). BL-197 adds the mail words and the
  "not available in this build" refusal for `gssapi` and `external`; this task removes the
  refusal for `external` only.
- Tests: `Surl.Authentication.UnitTests` (`PolicyFixture.cs`, `ManualTimeProvider.cs`),
  `Surl.Cli.UnitTests/CommandLineParserTests.cs`, `HelpTextTests.cs`, `ManualTextTests.cs`,
  `AiHelpFactsTests.cs`, `AiHelpTextTests.cs`,
  `Surl.Console.UnitTests/CommandLineRunnerAuthenticationTests.cs`.

## Acceptance criteria

- [x] Fast tests in `Surl.Authentication.UnitTests` build the client certificate in memory with
      `System.Security.Cryptography.X509Certificates.CertificateRequest` (no file, no store, no
      platform-specific API) and cover:
      - accepted: a certificate whose subject simple name matches an account, with an empty
        authorization identity and with one equal to the name, gives `Accepted` with
        `AccountName` the certificate's name, both with and without an initial response (without
        one, `BeginAsync` first returns a `Challenge` with an empty `Challenge`);
      - no certificate: with `TlsSession` `null` or `ClientCertificate` `null`,
        `GetMailLoginOffer` does not list `EXTERNAL`, and `StartSaslExchange` for `EXTERNAL` gives
        `RefusedMechanism` without the delay;
      - with a certificate, `GetMailLoginOffer` lists `EXTERNAL` last (ADR-0049 decision 2) when
        `external` is accepted, and not at all when it is not accepted;
      - no account of the certificate's name, and an authorization identity that differs
        (including by case only): `RefusedCredentials`, decided only after the 1-second delay
        has been advanced on the injected `TimeProvider`;
      - `--allow-anonymous`: `AcceptedUnchecked` on the first response, nothing checked, no note;
      - the note: the step that decided carries a `CheckedLogin` whose method is `EXTERNAL` and
        whose user is the certificate's subject simple name, on both `Accepted` and
        `RefusedCredentials`.
- [x] `CommandLineParserTests` pin that `--auth external` is accepted (no longer refused as
      "not available in this build") and that the default `AcceptedAuthenticationMethods` is
      `digest, cram-md5, basic, plain, login, bearer, oauthbearer, xoauth2, external, aws-sigv4`
      in that order; `gssapi` is still refused as not available.
- [x] `CommandLineRunnerAuthenticationTests` pin the warning line's order with `external` in it
      (after `xoauth2`, before `aws-sigv4`).
- [x] `--auth`'s `Default` in `CommandLineOptions.cs`, its explanation, the manual text and the
      `AiHelpProse` paragraph name `external` in the default set; `HelpTextTests`,
      `ManualTextTests`, `AiHelpFactsTests` and `AiHelpTextTests` pass.
- [x] `dotnet build Surl.Authentication.UnitLibrary -warnaserror`,
      `dotnet build Surl.Cli.UnitLibrary -warnaserror` and `dotnet build Surl.Console -warnaserror`
      are clean; `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs
      `TestCategory=Integration`.
- [x] `Measure-CodeQuality.ps1` reports 100% line and 100% branch coverage and no failing member
      for `Surl.Authentication.UnitLibrary`, `Surl.Cli.UnitLibrary` and `Surl.Console`.
- [x] Every new test is platform-neutral: no Windows-only path, store, error text or certificate.

## Notes

- The mail servers themselves (BL-200 SMTP, BL-204 IMAP, BL-206 POP3) only frame what this
  policy returns; this task does not touch them.
- Plan as built: `AuthenticationMethod.External` (between `XOAuth2` and `AwsSigV4`) joins
  `AuthenticationMethods.DefaultAccepted`; `SaslMechanism.InOfferOrder` ends with `EXTERNAL`;
  `SaslExchangeContext` carries `ClientCertificate`; `ExternalSaslExchange` compares the
  authorization identity's bytes with the name's UTF-8 bytes (ordinal, so invalid UTF-8 simply
  never matches) and checks `AccountBook.HasNamedAccount` (the empty-name Bearer account never
  matches). `AuthenticationPolicy.CanIdentifyClient` keeps `EXTERNAL` out of the offer and
  answers it `RefusedMechanism` when `TlsSession?.ClientCertificate` is `null`.
- Choice (sensible default): without a client certificate `EXTERNAL` is `RefusedMechanism` even
  under `--allow-anonymous`, since it is not offered there and has no identity to accept; the
  acceptance criterion names no exception. Recording it in ADR-0049 is BL-235:
  `Documentation/Planning/Decisions` was held by BL-155 in `Doing`, so this task did not widen
  its `touches` into it.
- Choice: `--auth`'s default grew to 79 characters, one word too long for a help line, so
  `HelpLayout.WrapParagraph` now breaks a word too long for any line after each comma; every
  other page is unchanged (the help tests pin them). Also in BL-235.
- `gssapi` still being refused as not available is pinned where the refusal lives, in
  `Surl.Console.UnitTests` (`RunAsync_AuthGssapi_WritesNotAvailableAndReturnsFailedInitBeforeAnyListenerBinds`);
  the parser reads both words and refuses neither, as before.
- `Measure-CodeQuality.ps1`: `Surl.Authentication.UnitLibrary` 100/100 (worst CRAP 10),
  `Surl.Cli.UnitLibrary` 100/100, `Surl.Console` 100/100, no failing member.

## Log

- 2026-09-29: Created.
- 2026-09-29: Filed by BL-185 (ADR-0049 decision 8).
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SASL EXTERNAL logs in as the verified TLS client certificate's name, offered last only with a certificate; --auth external is in the default set
