---
id: BL-1838
title: Correct the add_tls_srp_account refusal text plus the get_events description in surl-mcp
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1838 — Correct the add_tls_srp_account refusal text plus the get_events description in surl-mcp

## Goal

The texts `surl-mcp` sends to agents give the one reason `add_tls_srp_account` refuses a user, and name every
kind of event `get_events` answers and every filter it takes.

## Context

Found while running BL-1832 (checked against the code on 2026-11-27). Paths are under `Application/`.

**The TLS-SRP refusal.** `Surl.Mcp.UnitLibrary/AddTlsSrpAccountTool.cs` answers
`user <user> cannot be added: it holds ':' or already has a TLS-SRP account`, and the comment above it speaks of
"A ':' in the user name". Nothing refuses a `:` in a TLS-SRP user name: not `SurlServerTlsSrpAccounts.Add`
(`Surl.Api`), `ComposedStores.AddTlsSrpAccount` (`Surl.Composition.UnitLibrary/ComposedStores.AccountStores.cs`)
or `TlsSrpAccounts.Add` (`Cryptography/Surl.TlsSrp.UnitLibrary`), whose only `ArgumentException` for non-null
arguments is a name already known.

**Decided: the text is wrong and the behaviour stays.** Reasons, so no ADR is needed:

- `Surl.Api` documents a taken name as the only `ArgumentException` of `TlsSrpAccounts.Add`.
- `--tlsuser` accepts any non-empty text (`OptionArgumentReader.SrpUserName` in `Surl.Cli.UnitLibrary`), so
  refusing `:` on add would give one account two rules and narrow what works today.
- ADR-0132 amendment 1 makes a taken name the refusal of an add, and the control endpoint already answers
  `user <user> already has a TLS-SRP account` (`ComposedControlTarget.AddTlsSrpAccountAsync`).

So the refusal becomes `user <user> already has a TLS-SRP account`. `add_proxy_account`'s similar text is true
(`SurlServerProxyAccounts.Add` refuses `:`) and does not change. The existing text is pinned in
`Surl.Mcp.UnitTests/ProxyAndTlsSrpAccountToolsTests.TlsSrpAccount_AddThenRemove_AnswerTheUser`.

**The `get_events` texts.** The `Description` and the class `<summary>` in
`Surl.Mcp.UnitLibrary/GetEventsTool.cs` list exchanges, logins, store changes, faults and hook failures, filtered
by kind, scheme, listen URL, account and sequence. `EventToolFilter.ReadAllKinds` also names
`ConsumerGroupChangedEvent` and `CookieReceivedEvent`, both kept, and `EventToolFilter.EventProperties` also
takes `store` and `name`. `exchange_bytes` is never kept, which the input schema already says.

The description becomes: "Show the events a Surl server kept, oldest first: exchanges started and ended, logins
judged, store changes, faults applied, hooks failed, consumer group changes and cookies received, filtered by
kind, scheme, listen URL, account, store, name and sequence."
`ProgramTests.RunAsync_ToolsList_IncludesEachEventToolWithAnObjectInputSchemaAndADescription` compares
`tools/list` with the constant, not with a literal.

The host README and the library `CLAUDE.md` are BL-1832's and BL-1839's. BL-1833 corrects other tool
descriptions. No store or fault changes and no event is added: a test seeds, observes and resets exactly as
before, and only text an agent reads changes. Upstream curl has no say in an MCP tool's text.

## Acceptance criteria

- [ ] `AddTlsSrpAccountTool` answers `user <user> already has a TLS-SRP account` for a taken name, and neither
      that text nor the comment beside it mentions `:`.
- [ ] `ProxyAndTlsSrpAccountToolsTests.TlsSrpAccount_AddThenRemove_AnswerTheUser` asserts the new text.
- [ ] A test in `ProxyAndTlsSrpAccountToolsTests` shows that `add_tls_srp_account` with user `a:b` on a server
      started with TLS-SRP answers `{"user":"a:b"}`.
- [ ] `GetEventsTool.Description` is the sentence quoted in Context, and the class `<summary>` names consumer
      group changes, received cookies and the `store` and `name` filters.
- [ ] A test in `Surl.Mcp.UnitTests/GetEventsToolTests` asserts that the description contains
      `consumer group changes`, `cookies received` and `store, name`.
- [ ] The tests of `Surl.Mcp.UnitTests` pass with no `TestCategory=Integration`, and
      `dotnet build Application/Surl.Mcp.UnitTests -warnaserror` is clean.

## Notes

## Log

- 2026-11-27: Created.
