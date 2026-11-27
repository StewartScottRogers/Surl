---
id: BL-1939
title: Count a dns listen URL with --dns-transport tls or https as one that uses TLS under TLS-SRP
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Documentation/Planning/Decisions/ADR-0122-how-surl-serves-https-over-tls-srp.md]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1939 — Count a dns listen URL with --dns-transport tls or https as one that uses TLS under TLS-SRP

## Goal

`surl --dns-transport tls dns://127.0.0.1:0 --tlsuser a --tlspassword b` starts and serves DNS over TLS under TLS-SRP, as does `--dns-transport https`, instead of exiting 2.

## Context

Measured against the built `surl` while working BL-1926 (2026-11-27): both command lines write `surl: option --tlsuser: needs a listen URL that uses TLS` and exit 2; with an `https` listen URL beside the `dns` one they start. `Application/Surl.Cli.UnitLibrary/TlsSrpCombination.cs` (`UsesTls`) reads `ListenUrl.IsImplicitTls` before `Surl.Composition`'s `DnsTransportComposition` marks the `dns` listen URLs, and `dns` is not one of its upgradable schemes. ADR-0122 Amendment 5 says TLS-SRP secures every TCP listener, so `tls` and `https` count; `quic` is UDP and still does not. Record the rule as an ADR-0122 amendment if `touches` is widened to the decision file. The `dns` paragraph BL-1926 added to `AiHelpProse` ("TLS-SRP options need a listen URL that uses TLS, and a `dns` one does not count") and its DataRow in `AiHelpTextTests` change with it, as does any such `ManualText` sentence.

## Acceptance criteria

- [x] A named test in `Surl.Cli.UnitTests` shows a `dns` listen URL with `--dns-transport tls`, and one with `https`, accepted beside TLS-SRP options with no other listen URL.
- [x] A named test shows `--dns-transport quic` alone, and the default `udp,tcp`, still refused `needs a listen URL that uses TLS` (2).
- [x] The `dns` `--aihelp` paragraphs and `ManualText` say what the code then does.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- `TlsSrpCombination` now counts a `dns` listen URL when `--dns-transport` names `tls` or `https`.
  It reads the option itself because `DnsTransportComposition` marks the listen URLs only after the
  command line is read. Recorded as ADR-0122 Amendment 6 (decided by Claude under Stewart's delegation).
- `touches` widened to the ADR-0122 file for that amendment; no task in Doing named it.
- A list with `quic` beside `tls` or `https` counts; the QUIC listener still needs a certificate
  (Amendment 5, unchanged).
- Tests: `Parse_TlsSrpWithADnsListenUrlOnlyServingATlsTransportOnTcp_Serves` (4 failed before the fix),
  `Parse_TlsSrpWithADnsListenUrlOnlyServingNoTlsTransportOnTcp_IsRefusedAsFailedInit`,
  `Parse_TlsSrpWithADnsTransportAndNoDnsListenUrl_IsRefused`.
- Built `surl` on Windows: `--dns-transport tls` and `https` with `dns://127.0.0.1:0`, `--tlsuser` and
  `--tlspassword` start and serve; `quic` exits 2.
- Verify: build 0 warnings, format check clean, fast tests 30377 passed, 0 failed, 3 skipped.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. surl --dns-transport tls or https with a dns listen URL now starts under TLS-SRP with no other listen URL
