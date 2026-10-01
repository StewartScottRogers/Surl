---
id: BL-242
title: Decide how Kerberos logins are proved against pinned upstream curl with a KDC
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-241, BL-218]
touches: [Documentation/Planning/Decisions, Record-CurlExchange.ps1]
requirement: FR-046
created: 2026-09-30
completed: 2026-09-30
---
# BL-242 — Decide how Kerberos logins are proved against pinned upstream curl with a KDC

## Goal

An ADR, marked "Decided by Claude under Stewart's delegation", decides how `--negotiate` and SASL
`GSSAPI` from a pinned upstream curl build are proved end to end against `surl --keytab`, and, if
a KDC can be stood up without Stewart, records what the pinned build actually sends.

## Context

- Decision this follows: `Documentation/Planning/Decisions/ADR-0057-surls-kerberos-keytab-and-ap-req-check-for-negotiate-and-sasl-gssapi.md`.
  - decision 11: every Kerberos test in CI is a fast, hand-built test with no KDC; what CI cannot
    show is a real client's token. Proving `--negotiate` and SASL `GSSAPI` from pinned upstream curl
    needs a KDC the client's SSPI trusts, and on Windows that means configuring the machine's realm
    (`ksetup`), which a CI runner and an unattended lane should not do.
  - "What upstream curl 8.21.0 does (measured)": on the lane machine (not in a domain, no KDC),
    the Windows reference build (`C:\Program Files\Git\mingw64\bin\curl.exe`, SHA-256
    `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`) sends no Kerberos token
    (SASL `GSSAPI` exits 94; `--negotiate` fails `SEC_E_NO_CREDENTIALS`), and the unpatched build
    (`C:\UpstreamCurl\static-curl-8.21.0-windows-x86_64\curl.exe`) falls back to bare NTLM. The
    Linux and macOS reference builds have no Kerberos, SPNEGO or GSS-API feature.
  - decisions 8 and 9 were decided from the RFCs, MS-SPNG and curl 8.21.0's source at tag
    `curl-8_21_0` (`lib/curl_sasl.c`, `lib/vauth/krb5_sspi.c`), not from measured bytes: SPNEGO
    `mechTypes` leading with `1.2.840.48018.1.2.2`, whether `mutual-required` is set, whether a
    `mechListMIC` is sent, and SASL `GSSAPI`'s bare `InitialContextToken` without mutual
    authentication.
- The ADR decides, at least:
  - how a KDC is provided: e.g. a hand-built loopback KDC (AS and TGS exchanges for one test realm)
    as a test fixture, built by hand with the BCL and `Surl.Kerberos` (base class library only; no
    package, no MIT/Heimdal install unless it is decided as a Stewart-approved download), or an
    existing KDC Stewart runs;
  - how the pinned Windows client's SSPI is pointed at it (`ksetup /addkdc` and
    `ksetup /addhosttorealmmap` need administrator rights and change the machine; if the chosen way
    needs that, the ADR files a `-Assignee Stewart` task for him to run it rather than doing it in
    a lane);
  - whether the proof runs only as `TestCategory=Integration`, never in the fast set;
  - which `Record-CurlExchange.ps1` extension, if any, it needs (for example a switch recording a
    Negotiate or SASL `GSSAPI` exchange end to end against a started `surl`), and the task that
    builds it;
  - that once a KDC exists, the pinned build's actual SPNEGO `mechTypes`, `mutual-required` flag
    and `mechListMIC` (HTTP) and its `GSSAPI` token and security-layer answer (SASL) are measured,
    and that ADR-0057 decisions 8 and 9 are revisited by a new ADR or amendment if they differ.
- Rules: upstream curl is the only oracle and the Curl port never is (ADR-0003); only builds pinned
  in `UpstreamCurlBuilds.json` are run; downloading another build is Stewart's (root `CLAUDE.md`,
  "Decisions").

## Acceptance criteria

- [x] A new ADR under `Documentation/Planning/Decisions/`, with the next free ADR number, marked
      "Decided by Claude under Stewart's delegation", states: how the KDC is provided; how the
      pinned Windows client is configured to use it and who does that; whether the proof is
      `TestCategory=Integration` only; what `Record-CurlExchange.ps1` needs; and which later tasks
      build and run the proof.
- [x] `Documentation/Planning/Decisions/README.md` indexes the new ADR.
- [x] If the ADR's approach needs a machine change, a download or a package, a
      `-Assignee Stewart` task is filed for exactly that, and the ADR names its ID.
- [x] If a KDC was available during this task and the pinned build was measured, the ADR records
      the measured SPNEGO `mechTypes`, `mutual-required` and `mechListMIC`, and the SASL `GSSAPI`
      exchange, with the build's path, SHA-256 and curl version (8.21.0), and states for each
      whether ADR-0057 decisions 8 and 9 hold; any difference is recorded as an amendment of
      ADR-0057 and filed as a task. If no KDC was available, the ADR says so and names the task
      that will measure.
- [x] Any change to `Record-CurlExchange.ps1` keeps its refusal to run a binary not pinned in
      `UpstreamCurlBuilds.json`.

## Notes

- A `docs` task: no behaviour change in any library. Code for a KDC fixture or an integration test
  belongs to tasks this ADR files.
- Decided in ADR-0065 (Decided by Claude under Stewart's delegation): a hand-built loopback test KDC
  (`Surl.Kerberos.TestKdc`, realm `SURL.TEST`, `127.0.0.1:88` UDP and TCP) as a test fixture;
  Stewart maps the realm once with `ksetup` (BL-265, assignee Stewart); the proof is
  `TestCategory=Integration`, Windows only, Inconclusive without the mapping;
  `Record-CurlExchange.ps1 -KerberosTestKdc`.
- No KDC was available: checked 2026-09-30, the lane machine is a workgroup member
  (`PartOfDomain` False, `ksetup`: "not configured to log on to an external KDC", no
  `Lsa\Kerberos\Domains` key) and the lane is not an administrator. Nothing measured; BL-268 measures.
- `Record-CurlExchange.ps1` was not changed in this task (the extension is BL-267), so its
  pinned-build refusal is untouched.
- Filed: BL-265 (Stewart: ksetup), BL-266 (test KDC), BL-267 (recorder switch), BL-268 (measure),
  BL-269 (Integration proof).
- Choice: host names under `.surl.test` pinned with curl's `--resolve`, so no hosts-file edit is
  needed; `.test` is reserved by RFC 6761.

## Log

- 2026-09-30: Created.
- 2026-09-30: Filed by BL-217 (ADR-0057 decision 12).
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ADR-0065 decides how Kerberos logins are proved: hand-built loopback test KDC, Stewart's one-time ksetup mapping (BL-265), Integration-only Windows proof; BL-265 to BL-269 filed
