---
id: BL-1482
title: Decide one rule for adding an account whose name already exists, for Surl.Api, surl-mcp and the control endpoint
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0132-how-the-account-book-changes-while-the-server-runs.md]
requirement: none
created: 2026-11-20
completed:
---
# BL-1482 — Decide one rule for adding an account whose name already exists, for Surl.Api, surl-mcp and the control endpoint

## Goal

An amendment to ADR-0132 states one rule for adding an account (and a proxy or TLS-SRP account) whose user name already
exists - refused, or its password replaced - that `Surl.Api`, `surl-mcp` and the control endpoint will all follow, and the
alignment work is filed.

## Context

- Surly cookbook bug report `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-20T1836-Surl-bug-report.md` item 5: the control
  endpoint's `POST /v1/accounts` replaces the password (`204`), while `Accounts.Add` throws `ArgumentException` and
  `add_account` answers an error.
- Today, for all three account kinds:
  - ADR-0132 decision 3 (`Documentation/Planning/Decisions/ADR-0132-how-the-account-book-changes-while-the-server-runs.md`): "Adding a
    name that has an account throws `ArgumentException`, and the existing account is kept."
  - `Surl.Api`: `Application/Surl.Api/SurlServerAccounts.cs:31-43` (throws, as documented), `SurlServerProxyAccounts.cs`,
    `SurlServerTlsSrpAccounts.cs:24`.
  - `surl-mcp`: `Application/Surl.Mcp.UnitLibrary/AddAccountTool.cs:54-58` (`user <name> already has an account`),
    `AddProxyAccountTool.cs:50-53`, `AddTlsSrpAccountTool.cs:50-53`.
  - Control endpoint: `Application/Surl.Composition.UnitLibrary/ComposedControlTarget.cs:176-207` removes then adds (replace), as
    `Application/Surl.Control.UnitLibrary/IControlTarget.cs:87`, `:100` and `:113` document ("or replaces the password of one with
    the same name"); ADR-0124 decision 7's table gives `POST /v1/accounts` only `204`, and ADR-0185 decision 2 relies on the
    replace ("`ComposedControlTarget`'s replace-by-remove-then-add (a password change) needs").
- Weigh: a password-rotation test (the cookbook's batch covers one) needs some way to change a password; a replace publishes
  `AccountRemoved` then `AccountAdded` (ADR-0132 decision 6) and leaves open sessions logged in (decision 8); the
  configuration surface only grows and keeps today's defaults (root `CLAUDE.md`); refusing on the control endpoint would be a
  `409 conflict` (ADR-0124 decision 7's error codes).
- Decided by Claude under Stewart's delegation; no package, threshold or upstream curl download is involved. Upstream curl has
  no say here (server-side test surface only).

## Acceptance criteria

- [ ] `ADR-0132` gains an amendment, marked "Decided by Claude under Stewart's delegation", that states the rule for each account
      kind and each host, what is published, the answer each host gives (exception type, MCP result text, HTTP status and error
      code), and why.
- [ ] If the rule adds a way to change a password (a new `Surl.Api` member, MCP tool or endpoint), the amendment names it, its
      arguments and its answer.
- [ ] The amendment updates ADR-0185 decision 2's sentence on the replace if the rule changes it, and ADR-0124 decision 7's
      `POST /v1/accounts` row if its answers change.
- [ ] task-planner files the alignment work as tasks of at most two projects each (for example `Surl.Composition` with
      `Surl.Control` docs, `Surl.Api`, `Surl.Mcp.UnitLibrary`), each depending on this task, and their IDs are in this task's Log.
- [ ] No `.cs` file changes in this task.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
