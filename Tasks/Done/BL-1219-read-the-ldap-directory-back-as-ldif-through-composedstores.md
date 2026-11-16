---
id: BL-1219
title: Read the LDAP directory back as LDIF through ComposedStores, Surl.Api, Surl.Mcp and the control endpoint (ADR-0164 decision 9)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1194]
touches: [Surl.Protocol.Ldap.UnitLibrary, Surl.Protocol.Ldap.UnitTests, Surl.Composition.UnitLibrary, Surl.Composition.UnitTests, Surl.Api, Surl.Api.UnitTests, Surl.Mcp.UnitLibrary, Surl.Mcp, Surl.Mcp.UnitTests, Surl.Control.UnitLibrary, Surl.Control.UnitTests]
requirement: none
created: 2026-11-15
completed: 2026-11-15
---
# BL-1219 — Read the LDAP directory back as LDIF through ComposedStores, Surl.Api, Surl.Mcp and the control endpoint (ADR-0164 decision 9)

## Goal

A test reads the LDAP directory back as LDIF, `userPassword` values omitted, through all three hosts.

## Context

ADR-0164 decision 9 (decisions 2 to 4 apply unchanged); the store is seeded by ADR-0158. Set `touches` at claim time to the projects that hold the store, `Surl.Composition`, `Surl.Api`, `Surl.Mcp.UnitLibrary` and `Surl.Control.UnitLibrary`, or split the task if the board says they overlap.

## Acceptance criteria

- [x] The store is readable through `ComposedStores`, `Surl.Api`, `Surl.Mcp` and the control endpoint with the member, tool and route names ADR-0164 decision 9 gives; a read publishes no event, consults no fault and returns no secret.
- [x] Unit tests hold every touched library at the quality gates; `--aihelp` and docs name the new reads.

## Notes

- touches set at claim: Ldap, Composition, Api, Mcp (+UnitLibrary), Control and their tests. Route answers `text/ldif; charset=utf-8`; tool returns `{"ldif"}`; LDIF is `version: 1` then entries, lines folded at 76, non-safe values base64. Hosts always compose an LDAP server, so only a bare `ComposedStores` reaches NotFound.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. LDAP directory readable as LDIF through ComposedStores, Surl.Api, Surl.Mcp and the control endpoint
