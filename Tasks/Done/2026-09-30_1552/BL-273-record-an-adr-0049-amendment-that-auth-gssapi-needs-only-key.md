---
id: BL-273
title: Record an ADR-0049 amendment that --auth gssapi needs only --keytab since BL-218
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0049-the-mail-servers-sasl-and-apop-logins.md, Documentation/Planning/Decisions/README.md]
requirement: FR-046
created: 2026-09-30
completed: 2026-09-30
---
# BL-273 — Record an ADR-0049 amendment that --auth gssapi needs only --keytab since BL-218

## Goal

`Documentation/Planning/Decisions/ADR-0049-the-mail-servers-sasl-and-apop-logins.md` records, as
its Amendment 2, that since BL-218 SASL `GSSAPI` is built: `--auth gssapi` is accepted when
`--keytab` is given, refused with `surl: (2) --auth gssapi needs --keytab` without it, and
`GSSAPI` is offered first when accepted; and the ADR index row says the same.

## Context

- Found by BL-214 (documenting Phase 3 mail, 2026-09-30).
- BL-218 is Done (`Tasks/Done/2026-09-30_1309/BL-218-check-sasl-gssapi-logins-in-surl-authentication.md`,
  "SASL GSSAPI logs in with a Kerberos ticket checked against --keytab, offered first; --auth
  gssapi is no longer refused as not available"), with BL-240 adding `--keytab`.
- ADR-0049 still says `gssapi` waits on BL-218 in five places: decision 1's table (`GSSAPI` row,
  "BL-218, after BL-217 decides Kerberos", around line 142); decision 2 ("`GSSAPI` is not offered
  until BL-218 lands", around line 169); decision 3's table (`gssapi` row, "no; refused as not
  available until BL-218", around line 200); decision 3's bullet on the refusal
  `surl: (2) --auth <word> is not available in this build` ("covers `gssapi` alone, until BL-218
  lands", around line 229); and decision 4 ("Until then `--auth gssapi` is refused as not
  available, `GSSAPI` is never offered...", around line 240).
- What is true now: `Surl.Console/CommandLineRunner.cs` `FindOptionRefusal` refuses
  `--auth gssapi` without `--keytab` with `(2) --auth gssapi needs --keytab`
  (`KeytabComposition.IsGssapiWithoutKeytab`; exit `FailedInit`, 2); `gssapi` is not in the default
  `--auth` set (`AuthenticationMethods`, `CommandLineOptions`: default
  `digest,cram-md5,basic,plain,login,bearer,oauthbearer,xoauth2,external,aws-sigv4`).
  `Documentation/Planning/Decisions/ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md`
  decisions 1 and 9 are the decisions this amendment points to.
- The shape to follow is ADR-0049's own "Amendment 1" section and the `external` row of decision
  3's table, which kept the original text and added "(... BL-216 is done)".

## Acceptance criteria

- [x] ADR-0049 ends with `## Amendment 2 - ...` (dated 2026-09-30 or the day it is written, naming
      BL-218, BL-240 and ADR-0057 decisions 1 and 9) stating: `--auth gssapi` is available;
      it needs `--keytab`, refused otherwise with `surl: (2) --auth gssapi needs --keytab` (exit 2);
      it is not in the default set; `GSSAPI` is offered first when `gssapi` is accepted, on any
      connection, TLS or not; the "not available in this build" refusal no longer covers any
      `--auth` word.
- [x] Each of the five places listed in Context either states the current behaviour or carries a
      pointer to Amendment 2 (e.g. "BL-218 is done; see Amendment 2"); no sentence in ADR-0049 says
      `gssapi` is refused as not available or not offered in the present tense.
- [x] `Documentation/Planning/Decisions/README.md`'s ADR-0049 row no longer says `gssapi` is "not
      available until built", and mentions amendment 2.
- [x] Every statement in the amendment is checked against the code named in Context (a search of
      `Surl.Console`, `Surl.Authentication.UnitLibrary` and `Surl.Cli.UnitLibrary`), not against
      another document.

## Notes

- A `docs` task: no code changes. Upstream curl is not measured here; nothing about curl's
  behaviour is claimed beyond what ADR-0049 and ADR-0057 already record (curl 8.21.0).
- Done in-session rather than through `align-and-document`: two Markdown files, no names or code.
- Checked against code: `CommandLineRunner.FindOptionRefusal` (the `--keytab` refusal, then
  `FindUnavailableOption`, whose only entry is `--hostcert`, so no `--auth` word is "not available");
  `KeytabComposition.IsGssapiWithoutKeytab`; `CommandLineOptions` `--auth` `Default` (no `gssapi`);
  `SaslMechanism.InOfferOrder` (`GSSAPI` first) and `AuthenticationPolicy.GetMailLoginOffer` /
  `CanIdentifyClient` (offered whenever the Kerberos acceptor is composed, TLS or not).
- Also marked BL-217 and BL-218 done in decision 13's "who builds what" table.

## Log

- 2026-09-30: Created.
- 2026-09-30: Filed by BL-214.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ADR-0049 Amendment 2 records that --auth gssapi is available, needs --keytab and is offered first; the ADR index row says so
