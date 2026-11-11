---
id: BL-540
title: Serve xmpp and xmpps from the surl executable
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-539, BL-405]
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitLibrary, Foundation/Surl.Protocol.Abstractions.UnitTests, Application/Surl.Console, Application/Surl.Console.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-11
---
# BL-540 — Serve xmpp and xmpps from the surl executable

## Goal

`surl xmpp://127.0.0.1:0` starts the XMPP server with STARTTLS available when a certificate is configured, and
`surl xmpps://127.0.0.1:0` starts it with direct TLS (XEP-0368). The `--aihelp` topic and help category exist, and
the extension switch turns both off.

## Context

- Composition: BL-405's extended-server composition in `Application/Surl.Console/CommandLineRunner.cs`. TLS
  upgrade availability is passed as for the mail servers. `xmpps` is wrapped with `ImplicitTlsSchemeServer`
  (ADR-0010). XEP-0368 section 3 names ALPN `xmpp-client`; offer it on `xmpps` if the per-scheme ALPN list allows,
  or record why not in the task's Notes.
- `--aihelp` (root `CLAUDE.md`, "`surl --aihelp`"): a `HelpCategories` row with both schemes, an
  `AiHelpProse.TopicAbout` paragraph naming RFC 6120 as its oracle, an `AiHelpExamples` entry, and the topic list
  in `AiHelpTextTests` growing by one.

## Acceptance criteria

- [x] A named test finds `xmpp` and `xmpps` registered when the switch is on, and absent when it is off.
- [x] The `--aihelp` completeness tests named in root `CLAUDE.md` pass.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Console` and `Surl.Cli` keep 100%
      line and branch coverage.

## Notes

- 2026-11-05 (lane 4): Extended-server composition has moved out of `Surl.Console`'s `CommandLineRunner` into
  `Application/Surl.Composition.UnitLibrary`: an extended server joins as one entry in
  `ExtendedProtocolServers.Registrations` (ADR-0124, decision 2), and the switch test belongs in
  `Surl.Composition.UnitTests`. So this task touches both projects too. BL-803 (Doing) touches
  `Application/Surl.Composition.UnitLibrary` and `Application/Surl.Composition.UnitTests`, so the task goes back to
  Backlog until BL-803 frees them. `ExtendedSchemeDefaultPorts` in `Surl.Cli` already lists `xmpp` 5222 and
  `xmpps` 5223. The XMPP server lives in `Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary`.

- Cause: `CommandLineOptions.AddOutputFile` (the `-o` pairing; the task's `PairOutputFile` is the old name) set `FileName` but left the `UsesRemoteName` that `--remote-name-all` gives every new output entry, so the runner, which takes `-o -` as "no file", fell back to the remote name. It now clears `UsesRemoteName`, as curl's own `-o` parsing switches `GETOUT_USEREMOTE` off. So `--remote-name-all -o - u` now gives the same `UrlOutput` as a plain `-o - u` (FileName `-`, no remote name), which the runner already writes to stdout, and the second URL of the two-URL form keeps its remote name. The REF behaviour is the review's measurement of curl 8.22.0 (STD-004). Pinned in `Curl.Cli.UnitTests/RemoteNameAllStdoutOutputTests.cs` (the `-o -` before and after the URL, and the two-URL form).
- `Parse_RemoteNameAllWithOutput_KeepsTheOutputFileName` in `CommandLineRemoteNameOptionTests.cs` pinned the old flag (`UsesRemoteName` true beside `-o x`). It is renamed `..._KeepsTheOutputFileNameInPlaceOfTheRemoteName` and now expects false. That file was added to `touches`: no task in Doing on origin/work/dark-factory names it.
- Environment: this host has no .NET SDK 10.0.401 (only 10.0.100-rc.1 and 11.0.100), so `global.json` does not resolve. I built and tested with a temporary local `global.json` naming 11.0.100, reverted after each run and not committed. Curl.Cli.UnitTests: 4510 passed, 0 failed. Curl.Console.UnitTests: 3137 passed, 2 failed. The two failures are the Windows SSPI NTLM tests in `CurlCompositionNtlmTests.cs`, which use the host's real SSPI with no output option and are unrelated to this change; filed as BL-4951.
- Because of those two failures, `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` stops at its test step and could not measure. The production change is one assignment with no branch, so complexity and CRAP do not change, and the new tests reach the line.
- The Linux/macOS criterion is met by keeping the change platform-neutral: the tests are parse-level only, with no paths or files.
- 2026-11-11 (lane 1): Done. `xmpp` and `xmpps` are two entries in `ExtendedProtocolServers.Registrations` (`xmpps` is `ProtocolServerRegistration.ImplicitTls`). `xmpps` joins `TlsSchemes`; `xmpp` joins `ServerTlsComposition.UpgradableSchemes` (STARTTLS offered with a certificate). Added `ComposedProtocolServices.MailDomains` (from `--mail-domain`). ALPN: `TlsApplicationProtocols` now offers `xmpp-client` for `xmpps` (XEP-0368 s3), so `touches` gained `Foundation/Surl.Protocol.Abstractions.*` and `Foundation/Surl.Networking.*` (no Doing task overlapped). `--aihelp` xmpp topic, help category and example added. Decided by Claude under Stewart's delegation (small enough for no ADR; follows ADR-0114 and ADR-0124 decision 2). Flaky unrelated: WebTransport `ServeStreamAsync_StreamWhoseConnectWasRefused_IsResetWithBufferedStreamRejected` failed once in a full run, passed on rerun.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Backlog. Needs Application/Surl.Composition.UnitLibrary and .UnitTests, which BL-803 in Doing touches
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. surl serves xmpp (STARTTLS with a certificate) and xmpps (direct TLS, ALPN xmpp-client), with aihelp topic and extension switch
