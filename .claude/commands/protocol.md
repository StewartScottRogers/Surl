---
description: Bring one protocol server from empty scaffold to a working server, conformance-checked against pinned upstream curl.
argument-hint: <protocol library name, e.g. Gopher>
---
Implement the server in `Surl.Protocol.$ARGUMENTS.UnitLibrary`, for every URL scheme upstream curl uses to reach it.

This is the `/feature` pipeline aimed at one protocol server. Run `/feature`'s stages, with
these additions specific to a protocol server:

1. Before planning, confirm the target library and its `.UnitTests` twin exist in
   `Surl.slnx`. If not, invoke `new-project`.

2. `protocol-architect` must additionally settle, and state in the plan:
   - the exact scheme strings the server answers, lowercased, and whether the secure
     variant is the same server over a TLS-secured connection rather than a second server;
   - the wire conversation as a byte script, measured from a build pinned in
     `UpstreamCurlBuilds.json` with `Record-CurlExchange.ps1` — what upstream curl sends,
     what Surl sends back, in order, for the happy path and for each failure;
   - the `SurlExitCode` for every way the server can fail to start or serve, and the
     `CURLE_*` code upstream curl exits with for each failure Surl produces on purpose,
     from https://curl.se/libcurl/c/libcurl-errors.html;
   - which of upstream curl's options exercise this scheme, from
     https://curl.se/docs/manpage.html, and what the server must do for each.

3. `test-writer` builds the fake connection from that byte script. No test in the
   protocol's `.UnitTests` project may carry `[TestCategory("Integration")]` — needing a
   live client means the seam is wrong, so report it instead of tagging around it.

4. `protocol-implementer` writes the server. It references
   `Surl.Protocol.Abstractions.UnitLibrary` and the horizontal libraries ADR-0002 lists,
   and nothing else. A need for another protocol server's code is a stop-and-report, not
   a project reference.

5. `conformance-auditor` is mandatory here, not optional: a protocol server is not done
   until a pinned upstream curl build has completed every case in the byte script against
   it, with the exit codes and output the plan predicts. Never the Curl port (ADR-0003).

Report at the end: schemes answered, the DI registration added, test count, the upstream
curl options exercised, and any option of upstream curl's for this scheme that Surl does
not yet answer — as tasks filed by `task-planner`, not as a footnote.
