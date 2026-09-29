# Architecture Decision Records

One file per decision, named `ADR-NNNN-short-slug.md`. Numbers are assigned in
order and never reused.

An ADR is immutable once **Accepted**. A decision that changes does not get edited
— a new ADR supersedes it, and the old one is marked `Superseded by ADR-NNNN`.
The value of the record is that it shows what was believed *at the time*.

Write one when a choice is expensive to reverse, when a reasonable person would
have chosen differently, or when the reasoning would otherwise be lost. Routine
choices do not need one.

Surl's ADRs are numbered from 0001 on their own. An ADR in the Curl port is not a Surl
decision, and never evidence for one (ADR-0003); where Surl adopts a choice the port
made, a Surl ADR says so and gives its own reasons.

## Index

| ADR | Title | Status | Date |
| --- | --- | --- | --- |
| [0001](ADR-0001-adopt-slnx-and-shared-projects.md) | Adopt the `.slnx` solution format and shared projects for documentation, the task board and the Claude configuration | Accepted | 2026-09-28 |
| [0002](ADR-0002-mirror-the-curl-ports-project-map.md) | Mirror the Curl port's project map, with `Surl.Content` in place of a file protocol library | Accepted | 2026-09-28 |
| [0003](ADR-0003-upstream-curl-is-surls-only-oracle.md) | Upstream curl is Surl's only oracle; the Curl port is measured against Surl last | Accepted | 2026-09-28 |
| [0004](ADR-0004-the-listener-seam-and-the-exchange-context.md) | The listener seam and the exchange context: how a protocol server receives its transport and what it is told about each exchange | Accepted | 2026-09-28 |
| [0005](ADR-0005-surls-exit-code-table.md) | Surl's exit-code table: upstream curl's `CURLE_*` number wherever a server-side meaning carries over, 0 after Ctrl+C or SIGTERM | Accepted | 2026-09-28 |
| [0006](ADR-0006-hardening-for-internet-facing-use.md) | Hardening for internet-facing use: connection, time and size limits, exposure defaults, what a peer may learn, TLS 1.2 minimum | Accepted | 2026-09-28 |
| [0007](ADR-0007-the-phase-1-command-line-surface.md) | The Phase 1 command-line surface: parser conventions measured from upstream curl, the option table, listen-URL rules, exact output texts and the verbose-log format | Accepted | 2026-09-28 |
| [0008](ADR-0008-how-the-http-server-answers-get-head-and-refusals.md) | How the HTTP server answers GET and HEAD, and the status, fields and persistence of each refusal | Accepted | 2026-09-28 |
| [0009](ADR-0009-what-a-content-store-directory-listing-holds.md) | What a content-store directory listing holds: ordinal order, no unrequestable names, no link out of the root; dot-files left to ADR-0006's options | Accepted | 2026-09-28 |
| [0010](ADR-0010-the-server-side-tls-contract.md) | The server-side TLS contract: `IConnection.UpgradeToTlsAsync` and `TlsSession`, certificates from `--cert`/`--key` or a throwaway, ALPN `http/1.1`, client verification by `--cacert` | Accepted | 2026-09-28 |
| [0011](ADR-0011-how-the-dict-server-answers.md) | How the DICT server answers: the served root's files as its one database, ordinal headwords, `exact` and `prefix` strategies, a versionless banner and Surl's fixed reply table | Accepted | 2026-09-28 |
| [0012](ADR-0012-how-the-gopher-server-answers-a-selector.md) | How the Gopher server answers a selector: a percent-encoded path mapped by the content store, files sent byte for byte, menus with item types by extension, one error menu for refused and missing | Accepted | 2026-09-28 |
| [0013](ADR-0013-how-the-tftp-server-answers.md) | How the TFTP server answers: `blksize`, `timeout` and `tsize` accepted into an OACK, bytes unchanged in both modes, a 5-second retransmission timeout and 5 retries, ERROR 1 for refused and missing, ERROR 2 for writes | Accepted | 2026-09-28 |
| [0014](ADR-0014-how-the-mqtt-server-answers.md) | How the MQTT server answers: every publish kept as its topic's retained message, a one-shot subscribe ended by `DISCONNECT`, `CONNACK` 1 for another protocol level, and a bare close for every violation and limit | Accepted | 2026-09-28 |
| [0015](ADR-0015-how-the-content-store-applies-the-exposure-options.md) | How the content store applies the exposure options and the upload limit: hidden paths answered as absent, uploads refused or capped with the partial file deleted, writes as default seam members | Accepted | 2026-09-28 |
| [0016](ADR-0016-the-linux-and-macos-upstream-curl-builds-and-how-ci-obtains-them.md) | The Linux and macOS upstream curl builds: stunnel/static-curl's static 8.21.0 builds of the unpatched tag, downloaded and cached in CI, verified by SHA-256 before they run, conformance tests in the `test` job on Linux and macOS | Accepted | 2026-09-28 |
| [0017](ADR-0017-curl-se-8-22-0-windows-build-as-a-supplementary-build.md) | curl.se's 8.22.0 Windows build pinned as a supplementary build, used only for HTTP/2 and HTTP/3; 8.21.0 stays the reference release, and SMB still needs a build | Accepted | 2026-09-28 |
| [0018](ADR-0018-a-trailing-slash-after-a-file-and-a-served-root-that-is-not-a-full-path.md) | A trailing slash after a file, and a served root that is not a full path: `/file.txt/` answered as a path that does not exist, a root relative to the current directory refused at construction | Accepted | 2026-09-28 |
| [0019](ADR-0019-how-the-http-server-enforces-the-hardening-limits.md) | How the HTTP server enforces the hardening limits: `Server: surl` on every response, 408 and 431 for the head, 413 checked before method dispatch and while a chunked GET or HEAD body is discarded, a one-second deadline for every refusal, 503 for a connection past a limit | Accepted | 2026-09-29 |
