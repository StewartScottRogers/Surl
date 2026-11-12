---
id: BL-1024
title: Refuse an Azure bearer token over plain text unless --allow-plaintext-auth is given
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1023]
touches: [Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.AzureStorage.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-1024 — Refuse an Azure bearer token over plain text unless --allow-plaintext-auth is given

## Goal

A request on a plain-text Azure connection (`abfs`, or `az` under `--az-plaintext`) that carries
`Authorization: Bearer` is answered `401 InvalidAuthenticationInfo` with the challenge unless
`--allow-plaintext-auth` is given; Shared Key and a SAS without `spr=https` are still served over plain text.

## Context

- Second of the three tasks BL-427 was split into (see BL-1023).
- Decision: ADR-0109 decision 6, "A bearer token over plain text" (ADR-0032 section 3: a secret in the clear needs
  its own option; the Azure page requires HTTPS for OAuth), and decision 3.3 for a SAS with `spr=https`.
- Today `AzureStorageProtocolServer` reads `connection.TlsSession is null` only to name the endpoint scheme; nothing
  refuses a bearer token in the clear. The server needs to learn `--allow-plaintext-auth`: a constructor option set
  by the composition (as BL-890 decides for OCI); the composition passes it in
  `Application/Surl.Composition.UnitLibrary` where BL-1023 registers the server.
- Stand-in: the refusal is a login refused; it publishes the `LoginJudgedEvent` the policy publishes for any
  refused login.

## Acceptance criteria

- [ ] A named Azure test shows a Bearer token on a plain-text connection answered `401 InvalidAuthenticationInfo`
      with the challenge, and the same request served when the server is told plain-text logins are allowed and
      when the connection is TLS.
- [ ] A named Azure test shows Shared Key served over plain text.
- [ ] A named `Surl.Composition.UnitTests` test shows `--allow-plaintext-auth` reaching the Azure server.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-12: Backlog -> Doing.
