---
id: BL-1976
title: Add a dated amendment note to ADR-0053 saying 354 and 334 carry no enhanced status code
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0053-how-the-smtp-server-answers-upstream-curl.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1976 — Add a dated amendment note to ADR-0053 saying 354 and 334 carry no enhanced status code

## Goal

A reader of ADR-0053 decision 1 learns, from a dated note, that `354` and `334` carry no enhanced status code.

## Context

Found while working BL-1957 (2026-11-27), checked against the ADR and the code that day. Documentation only; no
behaviour, store, event or fault changes.

In `Documentation/Planning/Decisions/ADR-0053-how-the-smtp-server-answers-upstream-curl.md`, decision 1 (about lines
116 to 119) says enhanced status codes "start the text of every reply except the greeting and the replies to `EHLO`
and `HELO`, as RFC 2034 section 4 says". Two other replies carry none: `354 End data with <CR><LF>.<CR><LF>`
(decision 1's own table; `SmtpReplies.StartData`) and the `334` continuation of `AUTH` (ADR-0049 section 7;
`SmtpReplies.Continuation`). Both are in `Protocols/Mail/Surl.Protocol.Smtp.UnitLibrary/SmtpReplies.cs`.

RFC 2034 section 4 (https://www.rfc-editor.org/rfc/rfc2034.txt, read 2026-11-27) requires the codes on 2xx, 4xx and
5xx replies other than the initial greeting and any response to `HELO` or `EHLO`, and says 3xx replies are not
included. The table's `501 Syntax: EHLO <domain>` and `HELO` replies are responses to `EHLO` and `HELO`, so the
sentence's existing exception already covers them.

Add the note in the file's amendment form, a new `## Amendment 2 (<date>): ...` section after Amendment 1, naming who
decided it as Amendment 1 does. Recorded text is not rewritten.

## Acceptance criteria

- [ ] ADR-0053 has a dated Amendment 2 naming decision 1 and saying that `354` and the `334` continuation carry no enhanced status code.
- [ ] The amendment says why: RFC 2034 section 4 covers 2xx, 4xx and 5xx replies, not 3xx.
- [ ] The amendment says the `501 Syntax` replies to `EHLO` and `HELO` carry none under decision 1's existing exception.
- [ ] The diff to ADR-0053 only adds lines.

## Notes

## Log

- 2026-11-27: Created.
