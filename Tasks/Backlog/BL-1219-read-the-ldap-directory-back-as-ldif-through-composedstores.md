---
id: BL-1219
title: Read the LDAP directory back as LDIF through ComposedStores, Surl.Api, Surl.Mcp and the control endpoint (ADR-0164 decision 9)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1194]
touches: []
requirement: none
created: 2026-11-15
completed:
---
# BL-1219 — Read the LDAP directory back as LDIF through ComposedStores, Surl.Api, Surl.Mcp and the control endpoint (ADR-0164 decision 9)

## Goal

A test reads the LDAP directory back as LDIF, `userPassword` values omitted, through all three hosts.

## Context

ADR-0164 decision 9 (decisions 2 to 4 apply unchanged); the store is seeded by ADR-0158. Set `touches` at claim time to the projects that hold the store, `Surl.Composition`, `Surl.Api`, `Surl.Mcp.UnitLibrary` and `Surl.Control.UnitLibrary`, or split the task if the board says they overlap.

## Acceptance criteria

- [ ] The store is readable through `ComposedStores`, `Surl.Api`, `Surl.Mcp` and the control endpoint with the member, tool and route names ADR-0164 decision 9 gives; a read publishes no event, consults no fault and returns no secret.
- [ ] Unit tests hold every touched library at the quality gates; `--aihelp` and docs name the new reads.

## Notes

## Log

- 2026-11-15: Created.
