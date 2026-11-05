---
id: BL-438
title: Require token authentication on the OCI registry
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-437]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.OciRegistry.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-438 — Require token authentication on the OCI registry

## Goal

By default the OCI registry challenges with `WWW-Authenticate: Bearer realm=…,service=…,scope=…` and serves its
own token endpoint, which checks the accounts file's credentials. It then accepts only tokens whose scope covers
the repository and action. Anonymous pulls are allowed only under `--allow-anonymous`.

## Context

- Decision: BL-435's ADR, decision 4.
- Docker Token Authentication Specification: the challenge parameters, the token request
  (`GET <realm>?service=…&scope=repository:<name>:pull,push`) with Basic credentials, the
  `{"token":…,"access_token":…,"expires_in":…,"issued_at":…}` response, and `401 UNAUTHORIZED` /
  `403 DENIED` (distribution spec error codes).
- Credentials are checked through the `Surl.Protocol.Abstractions` contracts. Tokens come from a random source
  injected for tests, and expire on an injected `TimeProvider`.

## Acceptance criteria

- [ ] Named tests show:
      - an unauthenticated pull challenged with the three parameters;
      - a token issued for good credentials and refused for bad ones;
      - a pull accepted with a pull-scoped token;
      - a push with a pull-only token gets `DENIED`;
      - an expired token is refused;
      - an anonymous pull is allowed under `--allow-anonymous` and an anonymous push still refused.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
