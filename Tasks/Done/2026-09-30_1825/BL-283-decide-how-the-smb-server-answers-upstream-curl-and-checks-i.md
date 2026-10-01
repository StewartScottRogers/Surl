---
id: BL-283
title: Decide how the SMB server answers upstream curl and checks its NTLM session setup
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions, Record-CurlExchange.ps1]
requirement: FR-050
created: 2026-09-30
completed: 2026-09-30
---
# BL-283 — Decide how the SMB server answers upstream curl and checks its NTLM session setup

## Goal

An accepted ADR decides, from measurement of pinned upstream curl 8.21.0, every SMB version 1
message `Surl.Protocol.Smb` sends to what curl sends over `smb` and `smbs`, how shares map onto
the content store, and how the NTLM session setup is checked through the authentication contract
under ADR-0032, so the SMB codec, contract, login and server tasks can be built without a question.

## Context

- **What upstream curl sends** (`lib/smb.c` at tag `curl-8_21_0`, read 2026-09-30 - confirm by
  measurement): NetBIOS session framing; `SMB_COM_NEGOTIATE` offering the one dialect
  `NT LM 0.12`; `SMB_COM_SESSION_SETUP_ANDX` carrying a 24-byte LM response and a 24-byte NT
  response (NTLMv1: `Curl_ntlm_core_mk_lm_hash`/`mk_nt_hash` then `Curl_ntlm_core_lm_resp` over
  the 8-byte challenge from the negotiate response), the user, the domain (`DOMAIN/user` or
  `DOMAIN\user`, else the URL's host name) as ASCII strings; `SMB_COM_TREE_CONNECT_ANDX` to
  `\<host>\<share>` with service `?????`; `SMB_COM_NT_CREATE_ANDX`; `SMB_COM_READ_ANDX` (32 KiB
  at a time) or `SMB_COM_WRITE_ANDX` for `-T`; `SMB_COM_CLOSE`; `SMB_COM_TREE_DISCONNECT`. No
  directory listing, no Unicode strings, no signing. curl refuses to start without a user name
  (`CURLE_LOGIN_DENIED`, 67), reads status `0x00050001` (ERRDOS/ERRnoaccess) as
  `CURLE_REMOTE_ACCESS_DENIED` (9) and any other non-zero status as
  `CURLE_REMOTE_FILE_NOT_FOUND` (78), and fails an upload with `CURLE_UPLOAD_FAILED` (25).
- **Measure first (ADR-0003).** SMB builds: ADR-0030's static-curl 8.21.0 Windows build
  (`C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe`, SHA-256 `589C8E4D...2648`) - the
  Windows reference build has no `smb` - and, where a lane runs Linux, the Linux reference build.
  Extend `Record-CurlExchange.ps1` with a binary SMB mode (a scripted reply per request, request
  bytes recorded) if `-Raw` cannot answer an SMB exchange. At least: `smb://host/share/file -u
  user:pass` download; `DOMAIN/user`; `-T` upload; `smbs://` with `-k`; a missing share; a missing
  file; a refused login; no `-u`; a file larger than 32 KiB; `-v` (the default port, which ADR-0007
  says is 445 from upstream's source but unmeasured - confirm it).
- **Decide:**
  - the negotiate response (dialect index, security mode user-level with challenge/response, no
    signing, `MaxBufferSize`, capabilities, the challenge from an injectable random source,
    server time from the injected `TimeProvider`, and a domain name that says nothing about the
    host, ADR-0006 section 3);
  - how shares map onto the content store (e.g. one share name serving the root, or every
    top-level directory as a share), and what an unknown share gets;
  - the NTLMv1 session-setup check under ADR-0032: every login through the contract
    (ADR-0032 "Protocol servers not yet built" criterion 1 - a new contract member in
    `Surl.Protocol.Abstractions` for SMB's LM and NT responses, implemented in
    `Surl.Authentication` beside `NtlmV2Calculation`), no account means refused, and whether
    NTLMv1 over plain `smb://` - a weak response, not a clear password - needs a loosening option
    or an `--auth` word, weighed against `smbs://`; the LM response's role (checked, ignored, or
    refused when it is not the NT response); guest or anonymous sessions under `--allow-anonymous`;
  - the error status for each refusal in the form curl reads (DOS error class codes, since curl
    sends no `FLAGS2_NT_STATUS`), keeping ADR-0006 section 3's rule that a peer learns nothing
    about which accounts or paths exist;
  - uploads under `--allow-uploads` and `--max-filesize` (ADR-0006, ADR-0015), reads through
    `ContentStore`, ADR-0006's SMB rows (`--max-message`, idle timeout, maximum duration) and what
    each limit sends;
  - how `smbs` is served (ADR-0010: implicit TLS, through `ImplicitTlsSchemeServer` as `smtps` is);
  - the verbose and trace notes (ADR-0033), and the help category and `--aihelp` topic name
    (ADR-0034 decision 1, ADR-0046);
  - the SMB version 1 codec's home: in `Surl.Protocol.Smb` (the one server that speaks it) or its
    own hand-built library (root `CLAUDE.md`, "Decisions"). BL-290 is filed on the assumption it
    stays in `Surl.Protocol.Smb`; if the ADR decides otherwise, re-plan it through `task-planner`.
  - DES for the LM and NT responses: the BCL's `DES` (ECB), which .NET documents as supported on
    Windows, Linux and macOS (learn.microsoft.com "Cross-platform cryptography", 2026-08-03), with
    MD4 from `Surl.Cryptography`; BL-291 is filed on that basis.
- The command lines BL-300's conformance tests must prove, with the expected curl exit code for
  each and the platform builds each runs on.

## Acceptance criteria

- [x] A new ADR in `Documentation/Planning/Decisions/`, Status Accepted, "Decided by Claude under
      Stewart's delegation", records each measurement (build path, SHA-256, arguments, date,
      transcript excerpt) and decides every point in Context.
- [x] It lists the curl 8.21.0 command lines the SMB conformance task must prove, with the expected
      exit code for each.
- [x] `Documentation/Planning/Decisions/README.md` indexes the ADR; any `Record-CurlExchange.ps1`
      extension is described in the script's comment-based help.

## Notes

- ADR-0073. 27 cases measured 2026-09-30 against ADR-0030's static-curl 8.21.0 Windows build
  with `Record-CurlExchange.ps1 -Raw` and fixed SMB replies built by a throwaway PowerShell
  helper; the replies' hex is in the ADR so BL-296 to BL-298 can re-record. `-Raw` sufficed, so
  the script is unchanged (no help to update). Both NTLMv1 responses were reproduced with a
  throwaway C# file-based app over `Surl.Cryptography`.
- Key finding: curl's SMB client never notices the server closing while it waits for an answer
  (exit 28 under `-m`, a hang without it), so the server answers every request, refusals
  included, and closes only after a refused login, at a limit or at shutdown.
- Decisions taken as defaults: shares are the top-level directories (URL paths match HTTP's);
  a new `--auth` word `ntlmv1`, not in the default set, rather than widening `ntlm` (ADR-0039
  refused NTLMv1 for HTTP); LM response ignored; domain not matched; `AnonymousAuthenticationPolicy`
  answers SMB `AcceptedUnchecked` as it does SSH - BL-294's Notes now say so, since its Context
  said "refuses".
- Context's DES note was out of date: BL-291 built `Surl.Cryptography.Des` by hand because the
  BCL's `DES` refuses NTLMv1's weak keys; the ADR records that.
- `--directory` has no short form; the ADR's command lines use the long one.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ADR-0073 decides, from 27 measured exchanges with pinned curl 8.21.0, how the SMB server answers smb and smbs and checks NTLMv1 under --auth ntlmv1
