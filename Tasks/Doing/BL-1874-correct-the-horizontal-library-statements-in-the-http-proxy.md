---
id: BL-1874
title: Correct the horizontal library statements in the HTTP proxy's docs, ADR-0088 and the glossary
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary, Documentation/Planning/Decisions/ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1874 — Correct the horizontal library statements in the HTTP proxy's docs, ADR-0088 and the glossary

## Goal

The HTTP proxy library's `CLAUDE.md` and ADR-0088 no longer present a `Surl.Authentication` reference as allowed,
and the glossary's "hand-built primitive library" row covers every `Surl.Cryptography.<Name>.UnitLibrary`.

## Context

Found by BL-1836 and checked against the repository on 2026-11-27. Prose only. No store, event or fault changes.

- `Protocols/Http/Surl.Protocol.HttpProxy.UnitLibrary/CLAUDE.md`, the paragraph starting "This library references",
  says "ADR-0088 also allows `Surl.Authentication.UnitLibrary`". `ProtocolIsolationTests` (in
  `Foundation/Surl.Protocol.Abstractions.UnitTests`) forbids a protocol server that reference
  (`ForbiddenProtocolReferences_OtherProject_IsForbidden`), and the project file has none. The paragraph's list of
  five references matches the project file.
- `Documentation/Planning/Decisions/ADR-0088-how-surl-serves-the-http-connect-proxy-and-the-https-proxy.md`,
  decision 1's table, still lists `Surl.Authentication.UnitLibrary` in the proxy's "May itself reference" cell.
  Only Amendment 3 ("No reference to `Surl.Authentication`") says otherwise; the proxy reaches the checks through
  `IAuthenticationPolicy`. Keep what was decided readable and say at decision 1 that Amendment 3 supersedes that
  entry. BL-1836 adds a dated correction to ADR-0002's ADR-0088 amendment note; ADR-0002 is not edited here.
- `Documentation/Wiki/Glossary.md`, row "hand-built primitive library", names nine libraries and omits `MlKem`
  (ADR-0082), `Curve448` and `Sm2` (ADR-0147) and `Srp` (ADR-0122), all present under `Cryptography/`. Their main
  types are `MlKem768` and `MlKem1024`, `X448`, `CurveSm2`, and `Srp6a`. The row also says "How each composes into
  SSH stays in `Surl.Protocol.Ssh`", which is not so for these four: `Surl.Tls13` references MlKem, Curve448 and
  Sm2, and `Surl.TlsSrp` references Srp.

## Acceptance criteria

- [ ] The proxy's `CLAUDE.md` does not say any ADR allows it to reference `Surl.Authentication.UnitLibrary`, and
      still says it reaches the authentication checks through `IAuthenticationPolicy` (ADR-0088 Amendment 3).
- [ ] ADR-0088 decision 1 says beside its table that Amendment 3 removes the `Surl.Authentication.UnitLibrary`
      reference.
- [ ] The glossary row names all thirteen `Surl.Cryptography.<Name>.UnitLibrary` projects (not
      `Surl.Cryptography.UnitLibrary`), each added one with its deciding ADR.
- [ ] The row's "Name in code" cell adds `MlKem768`, `MlKem1024`, `X448` for Curve448, `CurveSm2` and `Srp6a`, and
      any statement in it about which libraries share a project is still true.
- [ ] The row limits "composes into SSH" to the libraries `Surl.Protocol.Ssh` composes, and says which libraries
      reference the other four.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
