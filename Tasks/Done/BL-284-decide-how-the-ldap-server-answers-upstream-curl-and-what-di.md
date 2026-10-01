---
id: BL-284
title: Decide how the LDAP server answers upstream curl and what directory it serves
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Record-CurlExchange.ps1]
requirement: FR-049
created: 2026-09-30
completed: 2026-09-30
---
# BL-284 — Decide how the LDAP server answers upstream curl and what directory it serves

## Goal

An accepted ADR decides, from measurement of the pinned Windows reference build (upstream curl
8.21.0 over `WinLDAP`), every LDAP message `Surl.Protocol.Ldap` sends to what curl sends over
`ldap` and `ldaps`, the directory the server searches (its model, where its entries come from,
its persistence under ADR-0031), and the binds it accepts under ADR-0032, so the directory, server
and registration tasks can be built without a question.

## Context

- **What upstream curl sends on Windows** (`lib/ldap.c` at tag `curl-8_21_0`, read 2026-09-30 -
  confirm by measurement): `wldap32` with protocol version 3, referrals off; with `-u` and the
  default `--basic`, `ldap_simple_bind_s`; with `--ntlm`, `--negotiate` or `--digest`,
  `ldap_bind_s` with `LDAP_AUTH_NTLM`, `LDAP_AUTH_NEGOTIATE` or `LDAP_AUTH_DIGEST` (SASL binds
  `wldap32` builds - record which mechanisms and tokens); with no `-u`, an
  `LDAP_AUTH_NEGOTIATE` bind with the current Windows user's credentials; a failed bind on
  `ldap://` retried once as LDAP version 2; then `ldap_search_s` with the URL's base DN, scope,
  filter and attributes (RFC 4516), and the entries written as `DN:` and attribute lines.
  `--ssl-reqd` on `ldap://` fails in curl with `CURLE_NOT_BUILT_IN` (4) ("explicit TLS not
  supported"); a failed bind is `CURLE_LDAP_CANNOT_BIND` (38); a failed search
  `CURLE_LDAP_SEARCH_FAILED` (39).
- `lib/openldap.c` (Linux and macOS curl normally; `STARTTLS`, root-DSE
  `supportedSASLMechanisms`, SASL binds through curl's SASL code) is not in any pinned build:
  BL-282 asks Stewart about a build, BL-287 pins it. Decide here what the server offers for it by
  RFC 4511, RFC 4513 and RFC 4422 (the `StartTLS` extended operation, `supportedSASLMechanisms` in
  the root DSE, SASL binds with the mechanisms `--auth` accepts, ADR-0049), so the server is built
  once; BL-287's measurement confirms or amends it.
- **Measure first (ADR-0003)** with the Windows reference build (`C:\Program
  Files\Git\mingw64\bin\curl.exe`, SHA-256 `0E773709...8778`), extending `Record-CurlExchange.ps1`
  with a binary LDAP mode (BER reply per request, request bytes recorded) if `-Raw` cannot answer:
  a base search; `one` and `sub` scopes; a filter with `&`, `|`, `!`, substrings, presence and
  `>=`; named attributes and `?*`; no entries; `-u user:pass`; `-u` with `--ntlm`, `--negotiate`,
  `--digest`; no `-u`; `ldaps://` with `-k`; `--ssl-reqd`; a refused bind (does the version 2 retry
  appear); a search refused; a large result.
- **Decide:**
  - the directory: its entry model (DN, object classes, attributes), DN parsing and normalisation
    (RFC 4514), matching rules (RFC 4517) for each filter item (RFC 4511 section 4.5.1), the root
    DSE, and where entries come from - e.g. an LDIF file (RFC 2849) under `<path>/.surl/ldap/`
    loaded at start after the data-directory lock (ADR-0031 decision 7), with a malformed one ending
    surl with `CouldNotReadFile` (37); what the in-memory mode starts with (ADR-0031 decision 2);
    whether an option names a seed file (a new option is a command-line decision, ADR-0007);
    bounds (entries, size limit, time limit) as ADR-0006 requires;
  - binds: simple bind checked through `CheckPasswordLoginAsync` (how the bind DN maps to an
    account name), refused unchecked on `ldap://` without `--allow-plaintext-auth` (ADR-0032
    criterion 3) with which result code; anonymous bind and searches with no bind under
    `--allow-anonymous`; SASL binds through the SASL contract (`IMailAuthenticationPolicy`,
    `MailLoginStep`, ADR-0049) - whether LDAP uses it as named or it is renamed protocol-neutral
    (if renamed, file the rename as its own task in `Surl.Protocol.Abstractions`,
    `Surl.Authentication` and the three mail servers, and add it to BL-309's `depends-on`); what
    `WinLDAP`'s NTLM, Negotiate and Digest binds carry and how each is checked (the Kerberos and
    NTLM checks in `Surl.Authentication` already exist, ADR-0039, ADR-0040, ADR-0057);
  - result codes and diagnostic messages for every refusal, keeping ADR-0006 section 3 (no
    account or entry enumeration beyond what the bound identity may read);
  - `StartTLS` (OID `1.3.6.1.4.1.1466.20037`) offered only with a certificate, as the mail
    servers' upgrades are (ADR-0010), `ldaps` through `ImplicitTlsSchemeServer`;
  - unbind, abandon, unknown operations (RFC 4511 section 4.1.1), ADR-0006's LDAP rows
    (`--max-message`, idle timeout, maximum duration) and what each limit sends;
  - the verbose and trace notes (ADR-0033), and the help category (`ldap` is one of curl's help
    categories, ADR-0034) and `--aihelp` topic;
  - the codec's home: BL-289 builds the BER message codec in `Surl.Protocol.Ldap` with the BCL's
    `System.Formats.Asn1` (BER rules), which `Surl.Kerberos` and `Surl.Authentication` already use,
    so no hand-built BER library is planned; if the ADR decides otherwise, re-plan through
    `task-planner`.
- The command lines BL-311's conformance tests must prove on Windows, with the expected exit code
  for each.
- Inputs: ADR-0006, ADR-0010, ADR-0031, ADR-0032, ADR-0033, ADR-0034, ADR-0046, ADR-0049, ADR-0050
  (the mail store's persistence pattern), RFC 4510 to RFC 4517, RFC 2849.

## Acceptance criteria

- [x] A new ADR in `Documentation/Planning/Decisions/`, Status Accepted, "Decided by Claude under
      Stewart's delegation", records each measurement (build path, SHA-256, arguments, date,
      transcript excerpt) and decides every point in Context, with the persisted byte format
      precise enough to pin in a test.
- [x] It lists the curl 8.21.0 command lines the Windows LDAP conformance task must prove, with the
      expected exit code for each.
- [x] `Documentation/Planning/Decisions/README.md` indexes the ADR; any `Record-CurlExchange.ps1`
      extension is described in the script's comment-based help.

## Notes

- Decided in ADR-0072 from 31 recorded cases against the Windows reference build, through the new
  `Record-CurlExchange.ps1 -Ldap` mode (plus `-LdapEntry`, `-LdapReply`, `-LdapIdleMilliseconds`
  and a general `-CurlTimeoutMilliseconds`, since `WinLDAP` ignores curl's `-m` and hung the first
  run). `-Raw` could not answer: `WinLDAP` needs its message IDs echoed.
- The finding that reshaped the plan: after an NTLM (Sicily or `GSS-SPNEGO`) or `DIGEST-MD5` bind,
  `WinLDAP` seals every later message (NTLM signature then 68 sealed bytes, the plain search's
  length; Digest insists on `qop=auth-conf`, `cipher=3des`). So the SASL contract needs a security
  layer: filed BL-328 (protocol-neutral contract, renaming `MailLoginStep`/`MailLoginOutcome` to
  `SaslLoginStep`/`SaslLoginOutcome`), BL-329 (NTLM sealing), BL-326 (`DIGEST-MD5` layers) and
  BL-327 (Low: Kerberos inside `GSS-SPNEGO`, unreachable from `WinLDAP` on loopback as measured);
  added BL-328, BL-329 and BL-326 to BL-309's `depends-on`.
- Other measured surprises recorded in the ADR: curl opens a silent connection before `WinLDAP`'s;
  `WinLDAP` resolves the host itself and ignores `--resolve`; `-k` does not reach `WinLDAP`, so
  `ldaps` fails 38 against an untrusted certificate; an empty password (`-u :`, `-u alice:`) still
  takes the current-Windows-user NTLM path, so the Windows build never binds anonymously.
- Defaults taken: the directory is read-only over LDAP (writes `unwillingToPerform`), loaded from
  `<path>/.surl/ldap/directory.ldif`, empty in memory, no seed option, no schema (matching rules
  by type); 10000 entries per search; version 2 binds answered like version 3 so curl's message
  names the real refusal.
- Measurement scratch (not committed): `%TEMP%\bl284\m01..m31`. No Python used.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ADR-0072 decides the LDAP server from 31 measured WinLDAP exchanges; Record-CurlExchange.ps1 -Ldap records them; BL-328 to BL-327 filed for the SASL security layer
