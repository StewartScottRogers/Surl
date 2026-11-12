---
id: BL-1052
title: Write the TLS-SRP verbose notes: a group outside RFC 5054 and which login failed
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests, Cryptography/Surl.TlsSrp.UnitLibrary, Cryptography/Surl.TlsSrp.UnitTests, Documentation/Planning/Decisions/ADR-0122-how-surl-serves-https-over-tls-srp.md]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-1052 — Write the TLS-SRP verbose notes: a group outside RFC 5054 and which login failed

## Goal

With `-v`, surl notes once at start each TLS-SRP group from `--srp-groups` that is not one of RFC 5054 Appendix A's, and after each failed TLS-SRP handshake whether the user was unknown or the password wrong, as ADR-0122 decision 4 asks.

## Context

- ADR-0122 decision 4 ("noted once in the verbose log"; "The verbose log says which it was; the
  wire never does") and Amendment 2 ("Not yet").
- `SrpGroup.IsRfc5054Group` (Surl.Cryptography.Srp) says whether a group is Appendix A's.
  `TlsSrpAccountsComposition` (Composition) reads the files; the start writes its `-v` notes through
  the `WriteStartLines` pattern the other compositions use.
- A failed handshake surfaces in `ServerTlsHandshake` (Networking) as `TlsHandshakeException`;
  `TlsSrpAccounts.Find` returns `IsSimulated` for an unknown user, so the handshake can carry that
  into the exception message the engine already logs.

## Acceptance criteria

- [x] `Surl.Composition.UnitTests`: a groups file with a group outside Appendix A writes one `-v` note naming its index, and an Appendix A group writes none.
- [x] `Surl.Networking.UnitTests`: replaying pinned upstream curl's `CurlTlsSrpBaduserBadpass` and `CurlTlsSrpJsmithBadpass` recordings gives handshake failures whose messages say "unknown user" and "wrong password" respectively, while the bytes sent stay identical.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and each touched library keeps 100% line and branch coverage.

## Notes

- Filed by BL-567 on 2026-11-11.
- Touches widened (rule 3; the only other Doing task, BL-652, touches Surl.Cli alone): `Cryptography/Surl.TlsSrp.UnitLibrary` and its tests, because only `TlsSrpServer` knows at the failure whether the account was simulated - `ServerTlsHandshake` never sees the user name; and ADR-0122, which records the notes as Amendment 3.
- Decision (ADR-0122 Amendment 3): `TlsSrpServer` rewrites the message of a `bad_record_mac` from the client's `Finished` record to `The TLS-SRP login failed: unknown user '<name>'.` or `... wrong password for '<name>'.` plus the record failure; the alert sent is unchanged, so the wire bytes are identical. Networking needed no code change: `ServerTlsHandshake` already carries the message into `TlsHandshakeException`; its replay test now asserts it.
- Decision: every `--srp-groups` group outside Appendix A is noted, used by a verifier or not, as `* --srp-groups: the group with index <i> is not one of RFC 5054 Appendix A's; OpenSSL's client refuses it`, from `-v` up, after the `--self-signed` lines (`ServerTlsComposition.WriteStartLines`).
- Coverage: Surl.TlsSrp and Surl.Networking 100/100 with no failing member; Surl.Composition 100% branch and every member within thresholds except `SurlServerComposition.ComposeServing` lines 360-362 (the coaps DTLS refusal, which this task did not touch and which the Console tests cover in the full merged measure). `TlsSrpAccountsComposition.Compose` was split into helpers to keep its Cobertura complexity at or under 10.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. surl -v notes each --srp-groups group outside RFC 5054 Appendix A, and a failed TLS-SRP handshake's log line says unknown user or wrong password
