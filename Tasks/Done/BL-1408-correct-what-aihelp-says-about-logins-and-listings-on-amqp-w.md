---
id: BL-1408
title: Correct what --aihelp says about logins and listings on redis, azure-storage and xmpp
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1401]
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-1408 — Correct what --aihelp says about logins and listings on redis, azure-storage and xmpp

## Goal

`surl --aihelp redis` says a password sent over `redis://` without `--allow-plaintext-auth` is answered `WRONGPASS`
whether it is right or wrong; `surl --aihelp azure-storage` says when a path-style `az://<ip>/<account>/...` segment
is read as an account and that listings need `--list-directories`; and `surl --aihelp xmpp` says an `xmpp` listen URL
needs a certificate unless `--allow-plaintext-auth` or `--allow-anonymous` is given.

## Context

- Surly cookbook bug report 2026-11-19 09:30, findings 6, 8 and 3. Help text only,
  `Application/Surl.Cli.UnitLibrary/AiHelpProse.cs`; each behaviour is decided and, for xmpp, built by BL-1401.
- **redis (finding 6) - decided, docs only.** ADR-0106 decision 4 (lines 191-196): `RefusedCredentials` "and
  `RefusedPlaintext` all answer, after the policy's refusal delay, `-WRONGPASS invalid username-password pair or user
  is disabled.`, naming neither account nor cause"; "The policy refuses a login on `redis://` without TLS unless
  `--allow-plaintext-auth` is given". Code: `RedisErrorReplies.cs:18`, `RedisLogins.cs:34`. Redis is an extended
  server (upstream curl has no `redis` scheme, ADR-0106 Context), so there is no pinned-curl measurement to make;
  the RESP specification defines no "encryption required" error. A distinct reply would leak no password (the
  policy refuses a clear-text login before checking it), but ADR-0106 chose one fixed reply on purpose, and Redis
  clients surface any `AUTH` error as a login failure either way. Today's `redis` topic (`AiHelpProse.cs:317`)
  says nothing about it.
- **azure-storage (finding 8) - decided, docs only.** ADR-0109 decision 2, "Path-style form": the first segment is
  an account only "when the `Host` name ... is an IP literal or `localhost`, and the path's first segment is the name
  of a non-empty account in the account set ... With no named accounts (anonymous or bearer only) the path-style
  form never applies." Decision 5.3 / line 594: "List Blobs needs `--list-directories`"; without it
  `403 AuthorizationPermissionMismatch` (`AzureListBlobs.cs:41`, `AzureListContainers.cs:45`,
  `AzurePathList.cs:48`, `AzureFilesystemList.cs:40`). Neither is in the `azure-storage` topic
  (`AiHelpProse.cs:106-112`), and the `content` topic's `--list-directories` list (`AiHelpProse.cs:87`) names S3 but
  not Azure.
- **xmpp (finding 3).** ADR-0114 decision 2; BL-1401 makes the start refuse with 58. The `xmpp` topic
  (`AiHelpProse.cs:369`) says "Without a certificate `xmpp` never offers TLS" and nothing of the refusal.
- Stand-in: a test author learns why a right password fails in clear, how to address a path-style blob with a
  bearer-only server (name the account with `--user <account>:<key>` or use the account-host form), how to turn
  listings on, and what an `xmpp` stand-in needs at start.

## Acceptance criteria

- [x] `surl --aihelp redis` states that over `redis://` without `--allow-plaintext-auth` every `AUTH` is answered
      `WRONGPASS`, a right password included, and that `rediss://` or `--allow-plaintext-auth` logs in.
- [x] `surl --aihelp azure-storage` states the path-style rule (IP literal or `localhost` host, first segment naming
      a configured account; otherwise the first segment is the container) and that List Containers, List Blobs and
      the Data Lake lists need `--list-directories`, else `403 AuthorizationPermissionMismatch`.
- [x] The `content` topic's `--list-directories` sentence (`AiHelpProse.cs:87`) names the Azure listings too.
- [x] `surl --aihelp xmpp` states that an `xmpp` listen URL without `--cert` or `--self-signed` exits 58 unless
      `--allow-plaintext-auth` or `--allow-anonymous` is given.
- [x] Tests in `Application/Surl.Cli.UnitTests/AiHelpTextTests.cs` pin each sentence; `dotnet build -warnaserror` is
      clean, the fast tests are green, and `Surl.Cli.UnitLibrary` stays at 100% line and branch coverage.

## Notes

- If `--manual` or `--help` text for `--list-directories` lists the servers it affects
  (`Application/Surl.Cli.UnitLibrary`, search `list-directories`), add Azure there too in this diff.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing.
- 2026-11-19: Doing -> Done. aihelp redis, azure-storage, content and xmpp topics corrected; tests pin each
