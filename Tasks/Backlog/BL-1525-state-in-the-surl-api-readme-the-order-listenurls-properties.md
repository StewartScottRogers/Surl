---
id: BL-1525
title: State in the Surl.Api README the order ListenUrls, properties, ConfigFiles and AdditionalArguments are read in
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Surl.Api]
requirement: none
created: 2026-11-20
completed:
---
# BL-1525 — State in the Surl.Api README the order ListenUrls, properties, ConfigFiles and AdditionalArguments are read in

## Goal

The Surl.Api README, and `SurlServerArguments`' class comment, say that a start reads `ListenUrls`, then each property that differs from its default, then `ConfigFiles`, then `AdditionalArguments`, so a single-valued option a config file sets (such as `max-filesize`) wins over the property and `AdditionalArguments` wins over both, while a repeatable one (`--user`, `--fault`) adds up.

## Context

Surly cookbook bug report 2026-11-20, item 5 (Surl `9ad60d80c`; unchanged at `299737d24`): `SurlServerOptions` properties such as `MaxFileSize` are applied before `ConfigFiles` and lose to them, while `AdditionalArguments` win, and the report found the order undocumented.

The behaviour is ADR-0099 decision 4 (properties spelled as `surl` options and parsed by `CommandLineParser`, left to right, so the last single-valued option wins), and it stays. It is stated only in one XML comment: `SurlServerOptions.ConfigFiles` (`Application/Surl.Api/SurlServerOptions.cs:234-240`, "after every property above and before AdditionalArguments, so AdditionalArguments can override a file and a file the properties"). It is missing from:
- `Application/Surl.Api/README.md`, the bullet list under the property table (around lines 26-60), where a reader looks first;
- `Application/Surl.Api/SurlServerArguments.cs:9-13`, whose class comment lists "each listen URL, then each property ..., then AdditionalArguments" and leaves out `ConfigFiles`, which `BuildArguments` adds between them (`SurlServerArguments.cs:188-189`) - a comment that says something untrue of the code.

Check "the last single-valued option wins" against `CommandLineParser` for one option (e.g. `--max-filesize` given twice) before writing it, and say which kinds add up instead (the repeatable item options and `--user`, `--fault`, `--config`).

No behaviour changes; this is a docs task. Stand-in: a test suite that keeps shared settings in a `-K` file and overrides one per test needs this order to know which wins.

## Acceptance criteria

- [ ] `Application/Surl.Api/README.md` states the four-step order, that a single-valued option given later wins, that repeatable options add up, and gives one example (`MaxFileSize` property vs. a `max-filesize` line in a config file vs. `AdditionalArguments`).
- [ ] `SurlServerArguments`' class `<summary>` names `ConfigFiles` in its place in the order.
- [ ] `SurlServerOptions.AdditionalArguments`' `<summary>` ("read after the properties above") also says it is read after `ConfigFiles`.
- [ ] `dotnet build Application/Surl.Api -warnaserror` is clean.

## Notes

## Log

- 2026-11-20: Created.
