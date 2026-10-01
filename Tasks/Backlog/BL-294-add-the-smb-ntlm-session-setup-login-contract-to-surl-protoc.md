---
id: BL-294
title: Add the SMB NTLM session-setup login contract to Surl.Protocol.Abstractions
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-283]
touches: [Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests]
requirement: FR-052
created: 2026-09-30
completed:
---
# BL-294 — Add the SMB NTLM session-setup login contract to Surl.Protocol.Abstractions

## Goal

`Surl.Protocol.Abstractions` carries the contract the SMB server asks to check an NTLM session
setup through - the login (user, domain, the server challenge, the LM and NT responses, whether the
connection is TLS), its verdict and the login note - as BL-283's ADR decides, so `Surl.Protocol.Smb`
and `Surl.Authentication` can be built against it in parallel.

## Context

- Decision: BL-283's ADR, and ADR-0032 "Protocol servers not yet built" criterion 1 ("a new contract
  member, added to Abstractions by that server's task, for any other kind (SASL, SSH public key,
  SMB's NTLM), implemented in `Surl.Authentication`"). The ADR names the members and types.
- Shape to copy: `ISshAuthenticationPolicy` with `SshPasswordLogin`, `SshLoginVerdict` and
  `SshLoginOutcome` (ADR-0051), and `CheckedLogin` carrying the login note (ADR-0038). Either a new
  interface (e.g. an SMB authentication policy) or a member on an existing one, as the ADR decides;
  `AnonymousAuthenticationPolicy` (the policy used where no accounts exist) gains whatever the new
  contract needs so it still refuses every login.
- Contract only: no implementation beyond `AnonymousAuthenticationPolicy`; `Surl.Authentication`
  implements it in BL-295, `Surl.Protocol.Smb` calls it in BL-296.
- Secrets in the login type are byte arrays or `ReadOnlyMemory<byte>`, never logged by `ToString`.

## Acceptance criteria

- [ ] The ADR's contract types exist in `Surl.Protocol.Abstractions.UnitLibrary` with XML doc
      comments naming the ADR.
- [ ] Tests in `Surl.Protocol.Abstractions.UnitTests` show `AnonymousAuthenticationPolicy` refuses
      an SMB login, and that the login type's `ToString` shows no response bytes.
- [ ] `dotnet build -warnaserror` is clean; the fast tests are green; `Measure-CodeQuality.ps1`
      reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Abstractions.UnitLibrary`.

## Notes

- Touches Abstractions, so it runs apart from every protocol task: keep it to the contract.
- ADR-0073 decision 3 names the types (`ISmbAuthenticationPolicy`, `SmbNtlmV1Login`,
  `SmbLoginOutcome`, `SmbLoginVerdict`) and corrects the Goal's Context on one point:
  `AnonymousAuthenticationPolicy` answers every SMB login `AcceptedUnchecked` with no note, as it
  answers every SSH login, rather than refusing it; the acceptance test shows that instead.

## Log

- 2026-09-30: Created.
