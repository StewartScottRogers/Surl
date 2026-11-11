---
id: BL-842
title: Record BL-717's Shared Key and SAS choices in ADR-0109
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-717]
touches: [Documentation/Planning/Decisions/ADR-0109-how-the-azure-storage-server-answers-az-abfs-and-abfss.md, Documentation/Conformance/Extended/AzureStorage.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-842 — Record BL-717's Shared Key and SAS choices in ADR-0109

## Goal

ADR-0109 carries an amendment, marked "Decided by Claude under Stewart's delegation", recording the choices
BL-717 made where decision 3 left them open, and the AzureStorage conformance matrix names the tests that now
cover Shared Key and shared access signatures.

## Context

- BL-717 built the checks (`Surl.Authentication`'s `AzureStorageVerifier`, the server's `AzureLoginJudge`) and
  lists each choice under its Notes. BL-717's `touches` did not include Documentation, so the ADR waits here.
- Choices to record: the `AzureStorageScope` contract on `HttpAuthenticationRequest` and the eight new
  `HttpAuthenticationRefusal` members; the SAS permission letters per operation (`c` alone never grants a write);
  `sr=d` served with `sdd`, `bs`/`bv` refused `UnservedSignatureKind`; each `AuthenticationErrorDetail` sentence
  (an unknown account reads `Signature did not match.`); an account whose password is not base64 cannot sign
  Shared Key or SAS; `+` in the query read as a space; credentials checked under `--allow-anonymous` for every
  Azure method (which BL-725 builds on).

## Acceptance criteria

- [ ] ADR-0109 has an amendment naming BL-717 that states each choice in Context, marked "Decided by Claude
      under Stewart's delegation".
- [ ] `Documentation/Conformance/Extended/AzureStorage.md` names `AzureStorageVerifierTests` and
      `AzureLoginJudgeTests` against the Shared Key and SAS rows.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
