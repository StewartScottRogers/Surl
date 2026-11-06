# ADR-0089 — How Phase 7 measures the Curl port against Surl beside pinned upstream curl

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-576. Built by BL-577 (the port pin), BL-578 (one case, compared and judged), BL-579 (client
  bytes), BL-580 to BL-590 (one family each), BL-591 (upstream test cases), BL-597 (the libcurl
  rows, filed by BL-576), BL-592 (summary and handoff) and BL-593 (the product documents).
- **Refines:** [ADR-0003](ADR-0003-upstream-curl-is-surls-only-oracle.md) decision 6, which this
  ADR puts into practice without changing it, and
  [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  decision 10's sentence about Phase 7 (decision 9 below). ADR-0003's oracle rule and ADR-0083's
  rules for the extended servers stand as written.

## Context

Phase 7 is the product overview's last phasing row and success criterion 5: "Then, and only then,
it measures the Curl port". ADR-0003 decision 6 says what it means: the port runs the same
conversations against Surl beside pinned upstream curl; wherever the two disagree, upstream is
right, the disagreement is the port's defect and is filed on the port's own board, and Surl never
changes to make the port pass.

What exists to build on, on 2026-11-04:

- The corpus: the `UpstreamCurl…SurlOver…Tests` classes in `Conformance/Surl.Conformance.UnitTests`
  run pinned upstream curl against `SurlOnLoopback`, the build chosen per protocol by
  `PinnedUpstreamCurl` (the Windows reference build; the supplementary static 8.21.0 build for HTTP/2
  and HTTP/3; the 8.22.0 build for SMB; the OpenLDAP build on Linux). The pinned-libcurl classes
  `PinnedLibcurlTalksToSurlOverWebSocketTests` and `PinnedLibcurlTalksToSurlOverRtspTests` drive the
  upstream `libcurl-4.dll` through `PinnedLibcurlDriver`. Phase 6 adds HTTP/2 and HTTP/3 classes
  (BL-371, BL-385) and the upstream test-case runner (BL-392 to BL-397, ADR-0080).
- The port's client: `CurlPortBuilds.json` pins its `curl` by path, SHA-256 and commit (ADR-0083
  decision 2); `CurlPortClientLocator` and `CurlPortClientRunner` in `Surl.Conformance.UnitLibrary`
  run it. Only `win-x64` is pinned. The port's own `CURL_EXTENSIONS=0` turns its extended schemes off
  (port `CLAUDE.md`).
- `surl` dumps nothing it receives; its `-v` notes land in `SurlOnLoopback.Log`.
  `ClientKexInitRecorder` and `DigestChallengeRelay` show the shape of a loopback relay.
- The port's board is `Tasks/` in `Z:\repos\Curl`, worked by its own dark factory with its own ID
  allocation. A commit to another repository is not one of root `CLAUDE.md`'s delegated git actions.

## Decision

### 1. The corpus

Phase 7 runs three sets of conversations, and leaves none out:

- **(a) Surl's own conformance conversations.** For each original protocol family, a new class
  `CurlPortBesideUpstreamCurlOver<Family>Tests` in `Conformance/Surl.Conformance.UnitTests` runs the
  same command lines, `surl` options and content as that family's `UpstreamCurl…SurlOver…Tests`
  class, one test per mirrored test, each naming the test it mirrors (`[Description("Mirrors
  UpstreamCurlTalksToSurlOverHttpTests.Get_ServesTheFile")]`). No existing class is edited. Families
  and tasks: HTTP/1.1 and HTTPS (BL-580), FTP and FTPS (BL-581), SCP and SFTP (BL-582), SMTP, IMAP and
  POP3 (BL-583), DICT, Gopher, TELNET, TFTP and MQTT (BL-584), WebSocket (BL-585), LDAP (BL-586), SMB
  (BL-587), RTSP (BL-588), HTTP/2 (BL-589), HTTP/3 (BL-590). A mirrored test that runs with
  `[OSCondition]` keeps its condition.
- **(b) Upstream curl's own test cases.** Every case ADR-0080's runner can run on the platform, with
  the port as the client in place of the reference build (BL-591).
- **(c) The libcurl rows.** The port's `Libcurl` is a managed API, not a native `libcurl-4.dll`, so
  `PinnedLibcurlDriver` cannot load it. Phase 7 gets port-side twins of the two driver scripts that
  take the same arguments and print the same one-line-per-call report, calling the port's `Libcurl`
  assembly pinned in `CurlPortBuilds.json` beside its `curl` (BL-597). BL-585 and BL-588 leave those
  rows to BL-597.

### 2. The pairing

Each case is compared with the upstream build its mirrored test uses on the same platform:
`PinnedUpstreamCurl`'s choice for (a), the case's reference build for (b), the pinned upstream
`libcurl-4.dll` for (c). Each client gets its own fresh `surl`, started with identical options over an
identical copy of the case's content, so neither run sees the other's state.

When the paired build is not 8.21.0 (the SMB build is 8.22.0), the finding names its version line. A
difference that upstream's release notes between 8.21.0 and that version describe as an upstream
change is judged **not comparable: release difference**, not a port defect. When no pinned upstream
build on the platform has a feature the case needs, the mirrored test is already Inconclusive, and so is
the Phase 7 test.

### 3. The port's environment

The port runs **as shipped**: `CURL_EXTENSIONS` is removed from its environment, and from upstream's for
symmetry. `surl` runs as shipped too: `SURL_EXTENSIONS` is removed from its environment. Neither switch
changes how an original scheme is served or requested (ADR-0083 decision 9, and the port's own rule), so
running both ways would double the runs and measure nothing new. Every other environment variable the
mirrored test sets is set identically for both clients; `HOME`, `CURL_HOME` and `USERPROFILE` point at
one empty folder per run, so neither client reads a `.curlrc` or `.netrc` the test did not write.

### 4. What is compared

| Item | Compared | Normalisation, and the case it serves |
| --- | --- | --- |
| Exit code | Exactly. | None. |
| Standard output | Byte for byte after normalisation. | The `surl` port number replaced by `<port>` (every case; each run has its own ephemeral port). A `Date:` or `Last-Modified:` field value under `-i` or `-D -` replaced by `<date>` (HTTP, RTSP). `-w` variables that measure time (`%{time_*}`, `%{speed_*}`) replaced by `<time>`. |
| Standard error | Only where the mirrored test asserts on it, and then only the lines it asserts on, normalised as standard output. | The progress meter is never compared: both clients run with `-s -S` or the mirrored test's own `-s`. The port is not held to upstream's wording where the mirrored test does not pin it. |
| Bytes each client sent to `surl` | Per connection, in order, on every plaintext TCP connection, recorded by a loopback relay (BL-579). | The `surl` and client ports replaced (FTP `PORT`/`EPRT`, `Host:`). A multipart boundary replaced by `<boundary>` everywhere it occurs (HTTP `-F`, SMTP MIME). Digest `cnonce` and the `response` computed from it replaced (HTTP, RTSP, SASL `DIGEST-MD5`). The NTLM client challenge, timestamp and the responses derived from them replaced (HTTP, SMB, SASL `NTLM`). `User-Agent:` replaced by `<user-agent>` (the port reports upstream's version, but its user agent is not a protocol requirement). |
| `surl`'s `-v` notes | Line by line after normalisation, from `SurlOnLoopback.Log`. | Ports as above; time stamps replaced by `<time>`. |
| Files `surl` stored | Byte for byte, where the mirrored test asserts on stored content (uploads). | None. |

Not compared, by design: request bytes inside TLS or SSH (the ClientHello, key exchange and every
encrypted byte depend on the TLS or SSH library, not on curl, and SSH and TLS randoms differ every run),
and request bytes over UDP (TFTP, HTTP/3), which the TCP relay does not see. For those, the exit code,
output, stored files and `surl`'s `-v` notes carry the comparison. An FTP data connection goes straight
to the port `surl` announced, so the relay records the control connection only; uploads are compared
through the stored file. Timing is never compared.

### 5. The verdict

Each case ends in exactly one of:

- **agreement** - every compared item equal after normalisation;
- **port defect** - upstream's run is what the mirrored test asserts, and the port's run differs in a
  compared item;
- **Surl defect** - upstream's own run is not what the mirrored test asserts. Filed on Surl's board,
  never on the port's; the port's run is not judged;
- **not comparable** - with a named reason: `release difference` (decision 2) or a reason a later ADR
  adds. A case missing a pinned file is Inconclusive, not a verdict.

A Phase 7 test **fails** when upstream's run is not what the mirrored test asserts (a Surl defect);
when a port defect appears that the family's findings file does not list; and when a listed finding
stops appearing. It **passes** on agreement and on a port defect the findings file lists. The findings
file therefore stays true of the code, and the suite is green while the port still has defects. Each
findings file has one table whose first column is the Phase 7 test's method name, which the test reads
(from the repository root, as `PinnedUpstreamCurl.RepositoryRoot` finds it).

### 6. The finding's form

One **Phase 7 finding** per disagreement, in
`Documentation/Conformance/CurlPort/<Family>.md` (`Http.md`, `Ftp.md`, `Ssh.md`, `Mail.md`,
`Dict-Gopher-Telnet-Tftp-Mqtt.md`, `WebSocket.md`, `Ldap.md`, `Smb.md`, `Rtsp.md`, `Http2.md`,
`Http3.md`, `Upstream-Test-Cases.md`, `Libcurl.md`). Each file names, once at its head, the port
commit and SHA-256, the upstream builds' SHA-256 and version lines, and Surl's commit it was measured
at. Each finding gives: the Phase 7 test and the test it mirrors; the command line; upstream's result
and the port's result for every item that differs (exit codes, the first differing output line, the
first differing request byte with its connection index and offset); and the specification section or
upstream documentation (with the curl version checked, 8.21.0) that says what the right behaviour is.
Upstream is right by ADR-0003 decision 6; the citation is there so the port's maintainer can fix the
defect without re-deriving it.

### 7. The handoff to the port's board

Surl sessions **never write into `Z:\repos\Curl`**. BL-592 publishes
`Documentation/Conformance/CurlPort/Port-Board-Handoff.md`: one section per port defect in the port's
task shape (title, goal, context, acceptance criteria), naming the finding it comes from and leaving
`id` and `touches` blank. The port's own `/task-plan`, run in a session of the port's repository,
files them: the port's lanes allocate IDs concurrently, and a commit to another repository is not
delegated to a Surl session. Stewart files it himself: he runs the port's `/task-plan` on the handoff,
so he sees each finding before it becomes the port's work (Stewart, 2026-11-04). Surl's part ends when
the handoff is committed and Stewart is told it is ready and how many findings it holds.

### 8. Platforms

Phase 7 runs on **Windows**. `CurlPortBuilds.json` pins only `win-x64`, lanes run on Windows, and
ADR-0003 named Git for Windows' 8.21.0 build "the right baseline for comparing the two in the last
phase". On Linux and macOS each Phase 7 test is Inconclusive with `CurlPortClientUnavailability`'s
message, as `UpstreamCurlUnavailability` reports a missing upstream build. CI does not run the port
(ADR-0083 decision 2). When a `linux-x64` or `osx-arm64` port pin is added, the same tests run there
with no change to this ADR, each platform's findings in its own section of the family's file.

### 9. The extended clients

ADR-0083 decision 10 says "Phase 7 then measures the port's extended clients against these servers
beside its original ones". Read with this ADR: upstream curl has no counterpart to an extended client,
so there is nothing to run *beside*. The port's extended clients meet Surl's extended servers in the
extended track's own `Integration` tasks (BL-422, BL-428, BL-434, BL-440, BL-446, BL-451, BL-456,
BL-462, BL-469, BL-477, BL-486, BL-493, BL-502, BL-508, BL-515, BL-523, BL-529, BL-534, BL-541),
judged by each protocol's specification, and Phase 7 does not run them again. A port defect they find
is recorded in that task's Notes; BL-592 writes each as a Phase 7 finding in decision 6's form, marked
"judged by the specification", in `Documentation/Conformance/CurlPort/Extended.md`, and puts it in the
same handoff (decision 7).
ADR-0083's oracle rule is unchanged: such a finding is about the port, never evidence about Surl.

### 10. The port commit measured

Phase 7 measures one port commit at a time: the one `CurlPortBuilds.json` pins, which every finding
file names. The pin moves only through a task that re-runs every family and updates every findings
file in the same change. Decision 5 makes that self-enforcing: a new pin that fixes a listed defect, or
adds one, turns its test red until the findings file says so. The extended servers' `Integration`
tests use the same pin.

## Consequences

Good:

- Every disagreement lands in a file that a test keeps true, with everything the port's maintainer
  needs to reproduce it, and Surl's suite stays green while the port still has defects.
- The port remains the thing measured: no Phase 7 output becomes an expected value, and a run where
  upstream itself fails is a Surl defect, not a port finding.
- No cross-repository write: the handoff is a file the port's own planner reads.

Costs and caveats:

- Request bytes inside TLS, SSH and UDP are not compared; a port defect visible only there is caught
  only if it changes the exit code, output, stored files or `surl`'s notes.
- Moving the port pin is a large task by design.
- Phase 7 is Windows-only until a non-Windows port pin exists.

## Alternatives considered

- **Compare the port with the expected values of the mirrored tests only, not with a live upstream
  run.** Rejected: success criterion 5 says "beside upstream curl", and a live upstream run in the same
  test separates a Surl regression from a port defect.
- **Run the port both as shipped and with `CURL_EXTENSIONS=0`.** Rejected: twice the runs for no
  original-scheme difference (decision 3).
- **File findings on the port's board directly.** Rejected: ID races with the port's lanes, and an
  undelegated commit to another repository.
- **Keep a permanently red test per port defect.** Rejected: a red suite hides new failures and blocks
  the dark factory's merge.
- **Compare TLS-wrapped request bytes by terminating TLS in the relay.** Rejected: the bytes inside
  would still differ by TLS library only where the library chooses (record sizes), and the plaintext
  twin of each conversation (`http` beside `https`, `ftp` beside `ftps`) already compares curl's own
  bytes.

## Amendment 1 (2026-11-05, BL-580): what the first family run showed decision 4 must leave out

Decided by Claude under Stewart's delegation, in BL-580, from the HTTP/1.1 and HTTPS family's first
runs. Each item below differed between two runs for a reason that is neither client's curl, so
comparing it would have filed a false port finding. None widens what a port finding may excuse: the
exit code, output, asserted stderr lines and stored files are still compared in every case.

- **Digest `nonce`** is replaced by `<nonce>` beside `cnonce` and `response`: it is the value each
  run's own `surl` chose at random, which the client only echoes (`RequestBytesNormalisation`).
- **Request bytes are not recorded** for AWS Signature Version 4 cases (`x-amz-date` and the signature
  over it follow the clock; decision 4 never compares timing) and for Kerberos and `Negotiate` cases
  (each token carries a fresh session key and time stamp, so its bytes are the GSS-API library's, as
  TLS bytes are the TLS library's).
- **`surl`'s packet dumps** (`#<n> > ` and `#<n> < ` lines under `-v`) are not compared on SSH: they carry
  the server's random cookie and padding. Its `*` notes are.
- **How a refused TLS connection ended** (`TLS handshake failed: …`, or an `IOException` from a reset
  connection) is not compared on `https`: the same client produced either note from run to run, and
  which one depends on the TLS library and timing.
- A case whose mirrored test runs a supplementary build, a relay of its own, several listeners or
  per-run arguments passes a `CurlPortRunSetup` that both runs share; set-up steps that are not the
  conversation measured (an SMTP delivery for an IMAP or POP3 read) run once through upstream curl, and
  each run's `surl` starts over its own copy of the result, so both clients meet the same bytes.

## Amendment 2 (2026-11-05, BL-585): a WebSocket client's key and masks

- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05, in
  BL-585.

Decision 4's request-byte normalisation gains two rules, for `ws` (the only WebSocket scheme the relay
records; `wss` is TLS):

- The `Sec-WebSocket-Key:` value is replaced by `<websocket-key>`. RFC 6455 section 4.1 makes it a
  fresh random nonce per upgrade request, so it differs between any two runs, upstream's own included.
- After the last WebSocket upgrade request on a connection, every client frame's payload is unmasked
  and its masking key zeroed (RFC 6455 section 5.3: the client chooses a fresh unpredictable key for
  every frame). What is compared is the frame as it would be under the key `00 00 00 00`: its FIN bit,
  opcode, length and payload stay compared byte for byte. A frame cut short is unmasked as far as it
  goes. Unmasking runs before every other rule, so the port replacement cannot shift a frame's bytes.

Why: measured in BL-585, every `ws` case's two runs differed first inside the key's value, and with
both rules every `ws` case agrees; without these rules no `ws` case could ever agree, and turning the relay
off for `ws` would compare none of curl's upgrade request or frames. `CurlPortResultNormaliser.NormaliseRequestBytes`
applies both (`WebSocketFrameUnmasking`, `RequestBytesNormalisation`).

## Amendment 3 — `surl`'s notes are read once settled, and in exchange order (2026-11-05)

Decided by Claude under Stewart's delegation, in BL-586.

LDAP showed two ways decision 4's notes comparison could give different verdicts from one run to the
next for the same two clients:

- **Read too early.** The notes were read the moment the client exited. A connection the client opened
  and dropped (the `WinLDAP` build's unused connection, a TLS handshake it abandoned) was noted only if
  `surl` got to it first. `PinnedCurlPortBesideUpstreamCurl` now reads the notes once they have stayed
  unchanged for 500 ms, waiting at most 5 s after the client exits.
- **Interleaving.** Two connections a client opens at once are served side by side, so which
  exchange's line `surl` writes first is the scheduler's choice, not the client's. Before comparing,
  the notes are grouped by exchange number (`#2 * …`). Each exchange's own lines, and the lines before
  the first exchange, keep the order `surl` wrote them.

Neither change hides a difference a client makes: an extra connection is still an extra exchange, and
each exchange's own lines are still compared in order. Without these changes, decision 5 could not hold:
a listed finding would come and go from run to run.

`PinnedCurlPortBesideUpstreamCurl.MeasureOnPortAsync` also lets a case name `surl`'s port before `surl`
starts, with both runs' `surl` listening on it one after the other, and set client environment
variables identically for both clients (decision 3). The Kerberos cases need this: their service
principal or `--resolve` entry names the port, and the MIT build reads a credential cache.

## Amendment 4 (2026-11-05, BL-589): HTTP/2's request bytes are compared decoded

Decided by Claude under Stewart's delegation, in BL-589, from the HTTP/2 family's first runs.

- **On plaintext HTTP/2 (`h2c`, by prior knowledge or by Upgrade), the bytes each client sent are
  compared as decoded frames, not byte for byte.** HPACK (RFC 7541) Huffman-codes `:authority`, which
  holds the port of each run's own recording relay, so two runs of the *same* client differ in raw bytes;
  and an encoder's indexing and Huffman choices are its own, not part of the request. The Phase 7 test
  renders each connection as text: an Upgrade request's head line by line, then one line per frame after
  the connection preface with its type, flags and stream, a `SETTINGS` frame's parameters, a `HEADERS`
  block's decoded fields (with `CONTINUATION`s, padding and priority removed) and a `DATA` frame's length.
  That text is normalised as decision 4 normalises request bytes (port, `User-Agent`, Digest) and
  compared; a difference is a port defect like any other compared item.
- **Not rendered:** every `WINDOW_UPDATE` but the connection's first, since how many follow, and where
  the `SETTINGS` acknowledgement falls among them, depend on how the client happened to read the answer
  (measured: upstream and the port sent the same frames, with the acknowledgement in a different place),
  and decision 4 never compares timing; and a `PING`'s opaque data.
- **`https` is unchanged:** `h2` by ALPN is TLS, and its bytes stay uncompared.
- The client's own ephemeral port in a `-v` line (`* Established connection to … from 127.0.0.1 port N`)
  is not normalised by `CurlPortResultNormaliser`, so such a line is not one of a case's compared standard
  error lines; the mirrored test's own count of it still judges upstream's run.

## Amendment 5 (2026-11-05, BL-584): DICT, Gopher, TELNET, TFTP and MQTT

Decided by Claude under Stewart's delegation, in BL-584, from that family's first runs. As in
amendment 1, each item differed between two runs for a reason that is not curl's protocol behaviour;
the exit code, output, asserted stderr lines, `surl`'s notes and stored files are still compared in
every case.

- **DICT's `CLIENT` text** is replaced by `<user-agent>`, as `User-Agent:` is: RFC 2229 section 3.6
  makes it the client's self-identification, which curl fills with `libcurl <version>`.
- **The random part of curl's MQTT client identifier** is replaced by `<client-id>`: a `CONNECT` from
  curl names `curl` and 8 alphanumerics drawn afresh for every connection.
- **Standard output has `surl`'s own port replaced** beside the client-facing (relay's) port: a reply
  can name the port the server listens on, as a Gopher menu does.
- **TELNET request bytes are not recorded.** How curl interleaves what it reads from standard input
  with its option negotiation depends on when `surl`'s options arrive: the same upstream build sent its
  input first against `surl` and its negotiation first in its recording. Timing is never compared.
- `CurlPortRunSetup.StandardInput` gives both clients the same standard input, as a TELNET session
  needs.

## Amendment 6 (2026-11-05, BL-582): what the SCP and SFTP family showed decision 4 must leave out

Decided by Claude under Stewart's delegation, in BL-582, from the SCP and SFTP family's first runs. As
with amendment 1, each item differed between two runs for a reason that is not curl's, and none widens
what a port finding may excuse: the exit code, output, asserted stderr lines and stored files are still
compared in every case.

- **A throwaway host key's fingerprint** is replaced by `<sha256>` and `<md5>` in `surl`'s notes (its
  `--throwaway-hostkey` warning and `Serving SSH host key` note): each run's `surl` makes its own key.
  Where curl's stderr line names that fingerprint (`--hostpubsha256` mismatch), the line is checked
  against the mirrored assertion on upstream's run and not compared.
- **SSH teardown notes** are not compared: `SSH disconnect received`, `SSH channel N ended`,
  `SFTP session ended`, `Exchange N ended`, and the SCP download's final acknowledgement note (`sent N
  bytes`, or `refused: read error` when the client closed the channel before acknowledging). They
  record how the client's SSH layer closes the channel and connection, inside the encrypted transport,
  and their order races the client's exit (the port closes the channel before it disconnects; libssh2
  disconnects alone, sometimes before the last acknowledgement). Every note up to the transfer -
  identification, negotiated algorithms, login, each SFTP or SCP request and its answer - is compared,
  read once `surl` has ended the exchange.
- **The client's `KEXINIT`** is compared where the mirrored test records it with
  `ClientKexInitRecorder`: it is sent in clear before the key exchange, and is the client's own offer,
  not a random. The identification line and the ten name-lists are compared line by line, beside the
  exit code and output.
- **A local file the client writes** (`-o` with `-C -` or `-R`) is per run and compared as a stored
  file: its bytes, and its modification time in round-trip form.
- **libssh2 1.11.1 WinCNG's key-exchange defect** (ADR-0062): a family that runs one SSH connection
  per run over some seventy cases trips its one-in-256 failure in most class runs. When either run
  exits 2 with exactly `curl: (2) Failure establishing ssh session: -8, Unable to exchange encryption
  keys`, the case is measured again, both runs, up to three attempts in all; ADR-0062 decision 3 names
  that failure, and only that one, as the client's defect a re-run clears. Every other failure is
  judged on the attempt it appears in.

## Amendment 7 (2026-11-06, BL-591): how corpus part (b) plays upstream's test cases with the port

Decided by Claude under Stewart's delegation, in BL-591, from the first run of decision 1(b). Part (b)
is not a mirrored test: each case's own `<verify>` section, written for upstream curl, is the
comparison, so decisions 4 and 5 are read for it as follows.

- **The cases.** Every case on the platform's passing list,
  `Documentation/Conformance/upstream-test-cases-passing.<platform>.txt` (ADR-0080 decision 5).
  `CurlPortBesideUpstreamCurlOverUpstreamTestCasesTests` plays each one twice, each run with its own
  fresh server halves and its own folder: with the build ADR-0080's runner pairs it with (the reference
  build, or the smb build), then with the port. Upstream's run is played in the same test, so the oracle
  is measured, not assumed from the list.
- **One client seam.** The runner takes an `IUpstreamTestCaseClient`: `UpstreamCurlTestCaseClient`
  wraps only an `UpstreamCurlRunner` (an upstream pin the locator verified), `CurlPortTestCaseClient`
  wraps only a `CurlPortClientRunner` (the port pin). Neither can be made from the other's location,
  and `UpstreamCurlLocator` still refuses the port's binary (ADR-0083 decision 2).
- **The port runs as runtests would run it.** runtests describes the curl it tests by that curl's own
  `--version`: `%VERSION` and `%VERNUM` are its version token (`servers.pm`'s `subvariables`), `%CURL`
  its path, and its `Protocols:` and `Features:` lines decide the `%if` features and which cases run.
  Each client is described by its own `--version` (`CurlVersionReport`), so the port's `User-Agent:
  curl/8.22.0` is matched against its own `%VERSION`, as upstream's suite would match it. This is not
  normalisation: it is the case as the case's author wrote it.
- **The verdict (decision 5).** Upstream did not pass: **Surl defect**, the port is not judged.
  Upstream passed and runtests would skip the case for the port (its build lacks the case's protocol,
  for example `smb`): **not comparable**, with the port's skip reason, a reason this amendment adds.
  Both passed: **agreement**. Otherwise **port defect**. The test fails on a Surl defect, an unlisted
  port defect, or a listed finding that no longer appears; `Upstream-Test-Cases.md`'s findings table
  names each case `test<N>` in its first column.
- **No normalisation beyond the case's own.** The `<verify>` section's strip lines, `mode="text"` and
  the rest apply to both runs alike (`UpstreamTestCaseVerifyChecker`), and nothing of decision 4's
  table is added: upstream's cases already substitute or strip what varies between runs (ports, dates,
  boundaries, the version) for whichever curl runs them.
