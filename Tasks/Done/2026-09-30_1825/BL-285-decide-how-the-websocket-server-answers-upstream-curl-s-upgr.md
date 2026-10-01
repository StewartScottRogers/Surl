---
id: BL-285
title: Decide how the WebSocket server answers upstream curl's upgrade and frames
priority: High
assignee: Claude
pipeline: docs
depends-on: [BL-281]
touches: [Documentation/Planning/Decisions, Record-CurlExchange.ps1]
requirement: FR-048
created: 2026-09-30
completed: 2026-09-30
---
# BL-285 — Decide how the WebSocket server answers upstream curl's upgrade and frames

## Goal

An accepted ADR decides, from measurement of pinned upstream curl 8.21.0, every byte
`Surl.Protocol.Ws` sends in the upgrade and after it - what it answers to curl's upgrade `GET`,
what messages it sends, and how it answers every frame curl can send (ping, pong, close, text,
binary, continuation, masked or not) - so the handshake, message and registration tasks can be
built without a question.

## Context

- **What upstream curl 8.21.0 does** (tag `curl-8_21_0`, read 2026-09-30 - confirm by
  measurement): `docs/internals/WEBSOCKET.md` says the upgrade is done from HTTP/1.1 or HTTPS;
  any answer but `101` is a transfer failure, `CURLE_HTTP_RETURNED_ERROR` (22), even a `2xx`;
  libcurl answers a `PING` with a `PONG` by itself unless `CURLWS_NOAUTOPONG`; frames are
  delivered whole up to a 64 KiB maximum; "Command line tool WebSocket ... has not been
  started". Neither `docs/cmdline-opts` nor `src/tool_listhelp.c` at the tag has a WebSocket
  option. So the `curl` tool upgrades and writes what it receives; only libcurl's API
  (`curl_ws_send`, `curl_ws_recv`, `CURLOPT_CONNECT_ONLY` 2, `CURLOPT_WS_OPTIONS`) sends text,
  binary, fragmented, ping and close frames of the caller's choosing.
- **Measure first (ADR-0003)** with the Windows reference build (`C:\Program
  Files\Git\mingw64\bin\curl.exe`, SHA-256 `0E773709...8778`), extending `Record-CurlExchange.ps1`
  with a WebSocket mode (answer the upgrade with a scripted head, then send scripted frames and
  record every client frame byte) - at least: `ws://` and `wss://` (`-k`); the exact upgrade
  request (fields, key, version, `User-Agent`, any `Sec-WebSocket-Extensions` or `-Protocol`);
  `-u` with `--basic`, `--digest`, `--ntlm`, `--negotiate` against a `401`; a `101` with a wrong
  `Sec-WebSocket-Accept` (does 8.21.0 check it); `200`, `400`, `426`; a text, a binary, a
  fragmented message, a 64 KiB and a larger frame, a frame the server wrongly masks, a `PING` (the
  masked `PONG`), a server `CLOSE` with code and reason (curl's answer and exit code), the server
  closing the TCP connection with no `CLOSE`; `-v`, `-i`, `--max-time`, `-o`.
- **libcurl.** Beside the reference `curl.exe` is `libcurl-4.dll` (present 2026-09-30 in
  `C:\Program Files\Git\mingw64\bin`), the library that `curl.exe` runs. Decide whether to pin it
  by SHA-256 in `UpstreamCurlBuilds.json` (the same build, already on disk - no download, so no
  question for Stewart) and drive `curl_ws_send`/`curl_ws_recv` from a C# file-based app or a
  `Record-CurlExchange.ps1` mode, so the client frames the tool never sends (client text, binary,
  fragmented, ping, close with a code) are measured from upstream and later proved; or record why
  not. The static Linux and macOS builds carry no shared library; say what that means for those
  platforms. Pinning it is not this task's edit: an entry that is a library, not a curl build,
  needs `Surl.Conformance.UnitLibrary`'s `UpstreamCurlBuildPins` and `UpstreamCurlLocator` and
  `Record-CurlExchange.ps1`'s pin check to know it. If the ADR decides to pin it, the task run has
  `task-planner` file that pin as its own `feature` task, and a follow-up that measures the
  client frames through it and amends this ADR with them, both before BL-304; until then the
  server's answers to client frames rest on RFC 6455, which dictates them.
- **Decide:**
  - the upgrade: which request `surl` accepts (RFC 6455 section 4.2.1: `GET`, HTTP/1.1, `Host`,
    `Upgrade: websocket`, `Connection: Upgrade`, a 16-byte base64 `Sec-WebSocket-Key`,
    `Sec-WebSocket-Version: 13`), the `101` head, and the refusal for each defect (`400`, `426`
    with `Sec-WebSocket-Version`, `404` for a path the content store has not, `405`), with
    `Server: surl` as ADR-0019 has; subprotocols and extensions (none offered, or which);
  - authentication: the upgrade's HTTP challenges through `IHttpAuthenticationSession` as the HTTP
    server does (ADR-0032 section 6); whether an anonymous upgrade is a read (allowed like an HTTP
    `GET`, ADR-0032 decision 2) or needs `--allow-anonymous`;
  - what surl sends once upgraded - e.g. the content-store file at the request path as one text or
    binary message and a `CLOSE`, an echo of every client data message, or both; how a directory
    path is answered (`--list-directories`, ADR-0006);
  - how every client frame is answered: `PING` with a `PONG` of the same payload, `PONG` ignored,
    `CLOSE` echoed with its code and then the connection closed, fragments reassembled, an unmasked
    client frame, a reserved bit or opcode, a control frame over 125 bytes or fragmented, invalid
    UTF-8 in text, each failed with its RFC 6455 section 7.4.1 close code;
  - ADR-0006's WebSocket rows: `--max-message` for a frame and a reassembled message (close code
    1009), idle timeout and maximum duration (close 1001 or 1008, and when), connection limits;
  - `wss` through `ImplicitTlsSchemeServer` (ADR-0010, ALPN `http/1.1`);
  - the verbose and trace notes (ADR-0033), and the help category and `--aihelp` topic names -
    curl 8.21.0's help categories have no WebSocket category (ADR-0034 decision 1).
- Where the upgrade head is read and written: BL-281's ADR (`Surl.HttpMessage` on the planning
  assumption). The frame codec is BL-288's, built from RFC 6455 alone; this ADR decides only the
  server's behaviour around it.
- The command lines (and libcurl cases, if pinned) BL-304's conformance tests must prove, with the
  expected exit code for each.

## Acceptance criteria

- [x] A new ADR in `Documentation/Planning/Decisions/`, Status Accepted, "Decided by Claude under
      Stewart's delegation", records each measurement (build path, SHA-256, arguments, date,
      transcript excerpt) and decides every point in Context, including the libcurl question.
- [x] It lists the curl 8.21.0 cases the WebSocket conformance task must prove, with the expected
      exit code for each.
- [x] `Documentation/Planning/Decisions/README.md` indexes the ADR; any `Record-CurlExchange.ps1`
      extension is described in the script's comment-based help; if `libcurl-4.dll` is to be
      pinned, the pin and measurement tasks exist on the board and BL-304 depends on them.

## Notes

- Delivered by the session itself rather than `align-and-document`: the decision rests on 54
  measurements made in this run, and writing the ADR beside them kept every number first-hand.
- `Record-CurlExchange.ps1 -Raw` gained `{WS_ACCEPT}` in a reply (the accept value of the last
  `Sec-WebSocket-Key` curl sent), documented under `-RawReply`. A whole `-WebSocket` mode was
  not needed: `-Raw`'s burst-by-burst replies already script a 101 and frames and record curl's
  `PONG`.
- Surprises measured: 8.21.0 ignores `Sec-WebSocket-Accept`; writes `CLOSE` and `PONG` payloads
  to stdout (so surl ends a download with an empty `CLOSE`); never answers a `CLOSE` and waits
  for the TCP close; refuses every `401` with 22 (only Basic, Bearer, SigV4 sent unasked log in).
- Decided: pin `libcurl-4.dll` (already on disk, no download). Filed BL-323 (pin and driver,
  feature) and BL-322 (measure and amend ADR-0071, docs); BL-304 now depends on both.
- Decided a new option `--ws-echo` (echo server for libcurl clients), built by BL-302, registered
  by BL-303 - both already say "as the ADR decides".
- Not measured: the Linux and macOS builds (lane runs Windows), and a server half-close after
  `CLOSE` (the recorder closes fully); ADR-0071 says BL-304 proves both.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. ADR-0071 decides, from 54 measurements of pinned curl 8.21.0, every byte the WebSocket server sends; Record-CurlExchange.ps1 -Raw answers an upgrade via {WS_ACCEPT}; libcurl pin filed as BL-323/BL-322
