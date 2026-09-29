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
| [0020](ADR-0020-how-surl-composes-https.md) | How surl composes https: the HTTP server registered for `https` in the composition root, TLS files read only for an implicit-TLS listen URL, the 58, 2 and 77 texts, and tests trusting a test CA with `--cacert` and `--ssl-no-revoke` | Accepted | 2026-09-29 |
| [0021](ADR-0021-a-lingering-close-for-every-tcp-connection.md) | A lingering close for every TCP connection: after FIN, `StreamConnection` reads and discards what the client sends, below TLS, until it half-closes or 2 seconds pass, so unread bytes do not turn the close into a reset | Accepted | 2026-09-29 |
| [0022](ADR-0022-an-accept-failure-of-one-client-does-not-stop-the-listener.md) | An accept failure of one client does not stop the listener: `ConnectionReset`, `ConnectionAborted` and Linux's pending network errors, and a socket lost before it became a connection, are absorbed and the listener accepts again; every other accept failure still ends `AcceptAsync` | Accepted | 2026-09-29 |
| [0023](ADR-0023-how-the-http-server-answers-a-file-system-failure.md) | How the HTTP server answers a file-system failure: an `IOException` or `UnauthorizedAccessException` reading a file's status answered 404 like a missing file, one after the 200 head an abort with a log note naming the file | Accepted | 2026-09-29 |
| [0024](ADR-0024-the-http-servers-drain-before-a-close-and-400-for-an-invalid-content-length.md) | The HTTP server's drain before a close, and 400 for an invalid `Content-Length`: after every closing response, unread request bytes are read and discarded for at most 1 second and 1 MiB; a `Content-Length` that is not one field of decimal digits is answered 400 and closed | Accepted | 2026-09-29 |
| [0025](ADR-0025-how-the-telnet-server-answers.md) | How the TELNET server answers: the server speaks first, `SGA` and `BINARY` performed and the `-t` options agreed, everything else refused with RFC 1143's no-reply rule, `SEND` answers noted on the log, each line echoed until `quit` then `bye` and a close once every `SEND` is answered, `line too long` past `--max-line` | Accepted | 2026-09-29 |
| [0026](ADR-0026-rtsp-conformance-on-macos-where-the-pinned-build-has-no-rtsp.md) | RTSP conformance on macOS, where the pinned `osx-arm64` build has no `rtsp`: a conformance test is Inconclusive where the current platform's pin does not list its scheme, so RTSP conformance runs on Windows and Linux; no second macOS build pinned | Accepted | 2026-09-29 |
| [0027](ADR-0027-the-http-server-sends-100-continue-before-it-reads-a-body.md) | The HTTP server sends 100 Continue before it reads a body: an HTTP/1.1 `GET` or `HEAD` whose `Expect` lists `100-continue` and whose body will be read gets `HTTP/1.1 100 Continue` first, so upstream curl sends it at once instead of after its one-second `--expect100-timeout` | Accepted | 2026-09-29 |
| [0028](ADR-0028-a-connection-refused-past-a-limit-is-noted-outside-any-exchange.md) | A connection refused past a limit is noted outside any exchange: `IExchangeLogFactory.NoteOutsideExchange` writes `#- * Refused a connection from <remote>: past --max-connections <n>.` (or `--max-connections-per-address`, or `a flow`) with `-v`; a log that throws does not stop the refusal | Accepted | 2026-09-29 |
| [0029](ADR-0029-the-live-task-board-page-reads-the-task-tree-and-a-board-branch-status-json.md) | The live task board page reads the task tree and a `board` branch `status.json`: `.github/board/site/index.html`, published by the Gource workflow at `.../Surl/board/`, draws `Tasks/` on `factory/phase-1` as a kanban from one unauthenticated tree call and one lane card per entry of the Curl port's `status.json` schema 1; a missing `status.json` shows no lane cards | Accepted | 2026-09-29 |
