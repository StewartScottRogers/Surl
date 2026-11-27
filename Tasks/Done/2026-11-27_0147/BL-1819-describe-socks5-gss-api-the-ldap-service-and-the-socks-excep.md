---
id: BL-1819
title: Describe SOCKS5 GSS-API, the ldap service and the SOCKS exception in the --keytab manual and AI help text
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1818, BL-1826, BL-1838]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-26
completed: 2026-11-27
---
# BL-1819 — Describe SOCKS5 GSS-API, the ldap service and the SOCKS exception in the --keytab manual and AI help text

## Goal

`surl --manual` and the `--keytab` paragraph of `surl --aihelp` say which listeners use the keytab, which service principals it serves, which logins need an account, and when the unused warning is written.

## Context

Found while working BL-1809. Both texts are in `Application/Surl.Cli.UnitLibrary`.

- `ManualText.cs`, the `--keytab <file>` paragraph (about lines 253 to 272), lists the services as `HTTP`, `smtp`, `imap` or `pop`. It leaves out `ldap` (ADR-0057 amendment 2; the `--aihelp` paragraph has it) and SOCKS5's `rcmd` (`SocksGssApiLogin.DefaultService`), and names only HTTP and the SMTP, IMAP and POP3 servers as users of the keys, leaving out the LDAP server's `GSS-SPNEGO` bind and SOCKS5 GSS-API (method `01`, curl's `--socks5-gssapi`).
- `AiHelpProse.cs`, the paragraph beginning "`--keytab <file>` reads Kerberos service keys" (about line 72), also leaves out `rcmd` and SOCKS5. The `proxy` topic paragraph about SOCKS already says them correctly.
- Both state the unused warning with no SOCKS exception. BL-1818 changes the behaviour (decision: the ADR-0057 amendment from BL-1817).
- Both say a Kerberos login "is accepted only when an account of exactly that name exists". Decision (BL-1816,
  ADR-0165 amendment 1, built by BL-1838): a SOCKS5 GSS-API login needs a proxy account (`-U`,
  `--proxy-user-file`; never `--user`) of exactly the principal's name when the proxy was started with proxy
  accounts, even an empty file; with none, any ticket the keys accept is accepted; `--allow-anonymous` skips
  the account match. The `proxy` topic paragraph about SOCKS does not say this yet.
- `ManualTextTests` and `AiHelpTextTests` in `Application/Surl.Cli.UnitTests` pin these texts (`ManualTextTests` pins the service-list line). The manual is laid out to 79 columns.
- Found by BL-1817: under `surl`, `refuse-login` reaches a SOCKS5 GSS-API login only when `-U` or
  `--proxy-user-file` is given, until BL-1826 is done; hence the dependency.
- Stand-in: help text only; no store, event or fault is involved.

## Acceptance criteria

- [x] The manual's `--keytab` paragraph lists `ldap` and SOCKS5's `rcmd` among the services, and says the LDAP server's `GSS-SPNEGO` bind and a `socks5`/`socks5h` listener's GSS-API login use the keys, the latter whatever `--auth` names.
- [x] The `--aihelp` `--keytab` paragraph names the `rcmd` service and says a `socks5`/`socks5h` listener's GSS-API login uses the keys whatever `--auth` names.
- [x] Both texts say the unused warning is not written when the listeners BL-1818 exempts are served, positionally or with `--proxy`.
- [x] Both texts limit "accepted only when an account of exactly that name exists" to the logins it is true of, and say which account, if any, a SOCKS5 GSS-API login needs as ADR-0165 amendment 1 does; the SOCKS `--aihelp` paragraph says it too.
- [x] Tests in `Surl.Cli.UnitTests` (`ManualTextTests`, `AiHelpTextTests`) pin the new sentences.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Text only, in `ManualText.cs` and `AiHelpProse.cs`. The manual's `--keytab` text is now two paragraphs, and
  its LDAP wording also names the LDAP server's SASL `GSSAPI` logins (ADR-0057 amendment 2).
- Sentences changed beside the `--keytab` paragraph because the new facts contradicted them: the SOCKS
  `--aihelp` paragraph ("asks for no login", "SOCKS5 then requires RFC 1929", "a login is refused unless
  `--allow-plaintext-auth`"), the manual's PROXY OPTIONS ("with none a proxy asks for no login"), and the
  `ldap` topic, which asked for a `--user-file` account where a `--user` one counts too.
- `--socks5-gssapi`, curl's option, joined the curl option names `OptionNames` and `AiHelpTextTests` allow.
- Tests: `AiHelpTextTests.Answer_Auth_*` (three), `Answer_Socks_SaysTheGssApiLoginIgnoresAuthAndWhichProxyAccountItNeeds`,
  and `ManualTextTests.Text_IsTheWholeManual`.
- Pipeline: plan, tests and edits were done in the session; one `code-reviewer` run checked every new sentence
  against the code, and its two must-fix and two smaller text findings are fixed. No byte on the wire changes,
  so no conformance stage ran and upstream curl was not measured.
- Follow-up: BL-1858 (found by the review: the AMQP servers offer SASL `GSSAPI` under the service `pop`).
- Verified 2026-11-27: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` clean, fast
  tests 30187 passed, 0 failed, 3 skipped.

## Log

- 2026-11-26: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. surl --manual and --aihelp now say which listeners use --keytab, which service principals it serves, which logins need an account, and when the unused warning is written.
