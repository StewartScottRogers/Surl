---
id: BL-1928
title: Count a dns listen URL with --dns-transport tls or https as one that uses TLS under TLS-SRP
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests]
requirement: none
created: 2026-11-27
completed:
---
# BL-1928 — Count a dns listen URL with --dns-transport tls or https as one that uses TLS under TLS-SRP

## Goal

`surl --dns-transport tls dns://127.0.0.1:0 --tlsuser a --tlspassword b` starts and serves DNS over TLS under TLS-SRP, as does `--dns-transport https`, instead of exiting 2.

## Context

Measured against the built `surl` while working BL-1926 (2026-11-27): both command lines write `surl: option --tlsuser: needs a listen URL that uses TLS` and exit 2; with an `https` listen URL beside the `dns` one they start. `Application/Surl.Cli.UnitLibrary/TlsSrpCombination.cs` (`UsesTls`) reads `ListenUrl.IsImplicitTls` before `Surl.Composition`'s `DnsTransportComposition` marks the `dns` listen URLs, and `dns` is not one of its upgradable schemes. ADR-0122 Amendment 5 says TLS-SRP secures every TCP listener, so `tls` and `https` count; `quic` is UDP and still does not. Record the rule as an ADR-0122 amendment if `touches` is widened to the decision file. The `dns` paragraph BL-1926 added to `AiHelpProse` ("TLS-SRP options need a listen URL that uses TLS, and a `dns` one does not count") and its DataRow in `AiHelpTextTests` change with it, as does any such `ManualText` sentence.

## Acceptance criteria

- [ ] A named test in `Surl.Cli.UnitTests` shows a `dns` listen URL with `--dns-transport tls`, and one with `https`, accepted beside TLS-SRP options with no other listen URL.
- [ ] A named test shows `--dns-transport quic` alone, and the default `udp,tcp`, still refused `needs a listen URL that uses TLS` (2).
- [ ] The `dns` `--aihelp` paragraphs and `ManualText` say what the code then does.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
