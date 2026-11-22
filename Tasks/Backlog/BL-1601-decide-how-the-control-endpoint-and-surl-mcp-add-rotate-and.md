---
id: BL-1601
title: Decide how the control endpoint and surl-mcp add, rotate and remove the Bearer-token account while the server runs
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0132-how-the-account-book-changes-while-the-server-runs.md, Documentation/Planning/Decisions/ADR-0136-how-the-control-endpoint-writes-events-and-reads-requests.md, Documentation/Planning/Decisions/ADR-0161-which-tools-surl-mcp-adds-to-delete-a-file-and-remove-an-account-and-why-it-adds-no-clear-faults.md, Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md]
requirement: none
created: 2026-11-22
completed:
---
# BL-1601 — Decide how the control endpoint and surl-mcp add, rotate and remove the Bearer-token account while the server runs

## Goal

ADR-0132 gains Amendment 2, decided by Claude under Stewart's delegation, stating whether and how the control
endpoint and `surl-mcp` add, change the password of (rotate) and remove the empty-name account - the Bearer-token
account `--user :tok` makes (ADR-0032 section 1) - and the ADRs whose tables it changes say the same.

## Context

- Surly bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-22T1308-Surl-bug-report.md` item 2 (question):
  with `surl --user :tok123 https://127.0.0.1:8443/` and the control endpoint, `POST /v1/accounts`
  `{"user":"","password":"tok456"}` answers `400 give user and password, neither empty`, `DELETE /v1/accounts/`
  answers `404`, and `surl-mcp` `add_account` with `"user":""` fails schema validation. A test that wants a token to
  expire mid-session cannot do it on a plain HTTP account; the cookbook works around it with the OCI token endpoint.
  The report was made against Surl `5da5b3eaf` with the Curl port as client, so it is a symptom report, not evidence.
- No ADR decides this on purpose. What exists today:
  - ADR-0032 section 1: "An empty name is a Bearer token", matched only by a Bearer token. BL-1586 (Done 2026-11-22)
    extended that to `PasswordLogin.IsToken` logins (NATS `auth_token`, Socket.IO `token`).
  - ADR-0132 and its Amendment 1 (BL-1482, rotation is `SetPassword` / `set_account_password` /
    `PUT /v1/accounts/{user}`) say nothing about the empty name.
  - ADR-0136 decision 7: "In `POST /v1/accounts`, `user` and `password` are both required and non-empty"; decision 8:
    `{user}` is a percent-decoded path segment, and `/v1/files/` with nothing after it is `404`. Neither weighs the
    Bearer-token account.
  - ADR-0161's table and ADR-0124 decision 8's tool table give `remove_account`'s `user` `minLength` 1.
- The code already lets the in-process host do it: `Foundation/Surl.Authentication.UnitLibrary/AccountBook.cs` documents
  `Remove` and `SetPassword`'s `userName` as "the empty name is the Bearer token's account", and
  `Application/Surl.Api/SurlServerAccounts.cs:33` (`Add`) refuses only a `:`. The refusals are in the host layers only:
  - `Application/Surl.Control.UnitLibrary/ControlStoreRequests.cs:220` (`AddAccountToAsync`, `string.IsNullOrEmpty(body.User)`);
  - `Application/Surl.Control.UnitLibrary/ControlEndpoint.cs:124-125` (the `["accounts", "{}"]` routes, which an empty
    segment does not reach);
  - `Application/Surl.Mcp.UnitLibrary/AddAccountTool.cs:27`, `SetAccountPasswordTool.cs:27`, `RemoveAccountTool.cs:28`
    (`"minLength": 1` on `user`).
- Options to weigh (the decision picks one and says why): (a) accept `user: ""` in `POST /v1/accounts` and the three
  MCP tools, and accept the empty last segment `PUT|DELETE /v1/accounts/` for the account book only; (b) the same for
  the body and tools, with dedicated `PUT|DELETE /v1/bearer-token` routes, since an empty path segment is easy to
  build by accident; (c) keep the refusal and record why a test rotates a token only through `Surl.Api`. Proxy and
  TLS-SRP accounts (ADR-0170) have no token form and are out of scope unless the decision says otherwise.
- Standing rules that bear on it: Surl is a stand-in (root `CLAUDE.md`): Controllable and "Configuration as data"
  (one model for the command line, `Surl.Api`, `Surl.Mcp` and the control endpoint; it only grows, defaults keep
  today's behaviour); secure by default (adding the token account while none existed makes Bearer logins succeed,
  which ADR-0124 decision 7's control-endpoint warning already covers). Upstream curl has no say: this is a
  server-side test surface (`--oauth2-bearer` is the client's side only).
- Stand-in: the store is the shared account book (ADR-0100 decision 4); events are `AccountAdded` / `AccountRemoved`
  with an empty `name`, as ADR-0132 decision 6 already says for every add and remove, never the token.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-0132-how-the-account-book-changes-while-the-server-runs.md` has
      "Amendment 2 (2026-11-22, BL-1601)" marked "Decided by Claude under Stewart's delegation", naming the options
      above, the one chosen and why, and a table of the exact request, tool argument and answer for add, rotate and
      remove of the empty-name account on `Surl.Api`, `surl-mcp` and the control endpoint, including what the
      published `StoreChangedEvent` names.
- [ ] The amendment states which answer an empty `user` gets for the proxy and TLS-SRP account kinds.
- [ ] ADR-0136 decision 7 (and decision 8 if a path changes), ADR-0161's `remove_account` row and ADR-0124 decision
      7's route table and decision 8's tool table are edited to agree with the amendment, each with a pointer to it;
      ADR-0132's header "Amended by" line names Amendment 2.
- [ ] If the decision is (a) or (b), BL-1602 and BL-1603 are edited to name the exact routes and arguments it chose
      (their acceptance criteria already assume option (a) and say how to adjust); if (c), BL-1602 and BL-1603 are
      moved to `Deferred` with the ADR as the reason.
- [ ] No `.cs` file changes; `dotnet build` stays clean.

## Notes

Answer to the Surly report's question goes in this ADR amendment; the cookbook reads it from there.

## Log

- 2026-11-22: Created.
